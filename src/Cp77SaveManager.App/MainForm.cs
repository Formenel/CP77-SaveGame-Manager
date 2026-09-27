using Cp77SaveManager.Core.Cleanup;
using Cp77SaveManager.Core.Configuration;
using Cp77SaveManager.Core.Models;
using Cp77SaveManager.Core.Retirement;
using Cp77SaveManager.Core.Scanning;

namespace Cp77SaveManager.App;

/// <summary>
/// 3-pane layout inspired by MMC snap-ins (Computerverwaltung etc.):
/// left = TreeView (Charakter -> Live/Storage), middle = ListView of saves for
/// the current tree selection, right = preview pane (screenshot + metadata)
/// for the currently selected save.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ConfigService _configService = new();
    private readonly SaveScanner _scanner = new();
    private readonly CleanupPlanner _planner = new();
    private readonly CleanupExecutor _executor = new();
    private readonly SaveActionService _actionService = new();
    private readonly SaveListSorter _sorter = new();

    private AppConfig _config = new();
    private IReadOnlyList<PlaythroughGroup> _groups = Array.Empty<PlaythroughGroup>();
    private bool _isLoadingList; // guards against re-entrant handlers while we rebuild the list

    private readonly TreeView _tree;
    private readonly ListView _list;
    private readonly PictureBox _screenshotBox;
    private readonly Label _metaLabel;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ContextMenuStrip _treeContextMenu;
    private readonly ContextMenuStrip _listContextMenu;
    private readonly SplitContainer _outerSplit;
    private readonly SplitContainer _innerSplit;

    private IReadOnlyCollection<string> ManagedRoots => new[] { _config.SaveDir, _config.StorageDir };

    public MainForm()
    {
        Text = "CP77 Save Manager";
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.Manual; // we place it ourselves from config

        var toolStrip = new ToolStrip();
        var reloadButton = new ToolStripButton("Neu laden") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        reloadButton.Click += (_, _) => LoadData();
        var settingsButton = new ToolStripButton("Einstellungen...") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        settingsButton.Click += (_, _) => OpenSettings();
        var cleanupAllButton = new ToolStripButton("Ausräumen (alle Charaktere)...") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        cleanupAllButton.Click += (_, _) => RunCleanup(_groups.Where(g => !g.IsUnknown).ToList());
        toolStrip.Items.AddRange(new ToolStripItem[] { reloadButton, settingsButton, new ToolStripSeparator(), cleanupAllButton });

        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Bereit.");
        _statusStrip.Items.Add(_statusLabel);

        // NOTE: SplitterDistance is intentionally NOT set here. A SplitContainer's
        // Width/Height before it's parented and sized is a small WinForms default,
        // and assigning a SplitterDistance larger than that throws an
        // ArgumentException immediately. It's set safely in the Shown handler
        // below, once the form has its real, final size.
        _outerSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        _innerSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        _tree.AfterSelect += (_, _) => UpdateListForSelection();

        _treeContextMenu = new ContextMenuStrip();
        var renameItem = new ToolStripMenuItem("Nickname ändern...");
        renameItem.Click += (_, _) => RenameSelectedCharacter();
        var cleanupOneItem = new ToolStripMenuItem("Ausräumen für diesen Charakter...");
        cleanupOneItem.Click += (_, _) =>
        {
            if (GetSelectedGroup() is { } group) RunCleanup(new[] { group });
        };
        var retireItem = new ToolStripMenuItem("Charakter in Rente schicken...");
        retireItem.Click += (_, _) => RetireSelectedCharacter();
        _treeContextMenu.Items.AddRange(new ToolStripItem[] { renameItem, cleanupOneItem, new ToolStripSeparator(), retireItem });
        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.Node.Tag is PlaythroughGroup)
            {
                _tree.SelectedNode = e.Node;
                _treeContextMenu.Show(_tree, e.Location);
            }
        };

        _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, ListViewItemSorter = _sorter };
        _list.Columns.Add("Typ", 110);
        _list.Columns.Add("Name", 110);
        _list.Columns.Add("Level", 50);
        _list.Columns.Add("Spielzeit", 80);
        _list.Columns.Add("Zeitpunkt", 130);
        _list.Columns.Add("Quest", 160);
        _list.Columns.Add("Größe", 70);
        _list.SelectedIndexChanged += (_, _) => UpdatePreview();
        _list.ColumnClick += (_, e) => SortByColumn(e.Column);

        _listContextMenu = new ContextMenuStrip();
        var storeItem = new ToolStripMenuItem("Storen (ins Storage-Dir verschieben)");
        storeItem.Click += (_, _) => StoreSelectedSave();
        var deleteItem = new ToolStripMenuItem("Löschen...");
        deleteItem.Click += (_, _) => DeleteSelectedSave();
        _listContextMenu.Items.AddRange(new ToolStripItem[] { storeItem, deleteItem });
        _list.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _list.GetItemAt(e.X, e.Y);
            if (hit is null) return;
            hit.Selected = true;

            var save = (SaveEntry)hit.Tag!;
            storeItem.Enabled = save.Location == SaveLocation.Live;
            _listContextMenu.Show(_list, e.Location);
        };

        var previewPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _screenshotBox = new PictureBox
        {
            Top = 8,
            Left = 8,
            Width = 300,
            Height = 169, // 16:9, matches CP77's screenshot aspect ratio
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };
        _metaLabel = new Label { Top = 186, Left = 8, Width = 300, AutoSize = false, Height = 420, TextAlign = ContentAlignment.TopLeft };
        previewPanel.Controls.Add(_screenshotBox);
        previewPanel.Controls.Add(_metaLabel);

        _innerSplit.Panel1.Controls.Add(_list);
        _innerSplit.Panel2.Controls.Add(previewPanel);
        _outerSplit.Panel1.Controls.Add(_tree);
        _outerSplit.Panel2.Controls.Add(_innerSplit);

        Controls.Add(_outerSplit);
        Controls.Add(toolStrip);
        Controls.Add(_statusStrip);

        Load += (_, _) => LoadData();
        Shown += (_, _) => ApplyWindowConfigAndSplitters();
        FormClosing += (_, _) => SaveWindowAndListConfig();
    }

    // -------------------------------------------------------------------
    // Window / list persistence
    // -------------------------------------------------------------------

    private void ApplyWindowConfigAndSplitters()
    {
        var w = _config.Window;
        var savedBounds = new Rectangle(w.X, w.Y, w.Width, w.Height);

        // Sanity check: only trust the saved position if it actually overlaps a
        // currently connected monitor (e.g. an external monitor that's no
        // longer plugged in shouldn't strand the window off-screen forever).
        var visible = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(savedBounds));
        if (visible && w.Width >= MinimumSize.Width && w.Height >= MinimumSize.Height)
        {
            Bounds = savedBounds;
        }
        else
        {
            // Fallback: center a sane default size on the primary monitor.
            // (StartPosition only matters before the form is first shown, and
            // this runs from the Shown handler, so we set Bounds directly.)
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
            var size = new Size(1100, 700);
            Bounds = new Rectangle(
                area.X + Math.Max(0, (area.Width - size.Width) / 2),
                area.Y + Math.Max(0, (area.Height - size.Height) / 2),
                size.Width,
                size.Height);
        }

        if (w.Maximized)
        {
            WindowState = FormWindowState.Maximized; // maximizes on whichever monitor Bounds/Location falls in
        }

        _outerSplit.SplitterDistance = Math.Clamp(260, _outerSplit.Panel1MinSize, Math.Max(_outerSplit.Panel1MinSize, _outerSplit.Width - _outerSplit.Panel2MinSize));
        _innerSplit.SplitterDistance = Math.Clamp(380, _innerSplit.Panel1MinSize, Math.Max(_innerSplit.Panel1MinSize, _innerSplit.Width - _innerSplit.Panel2MinSize));

        var widths = _config.SaveList.ColumnWidths;
        if (widths is not null && widths.Count == _list.Columns.Count)
        {
            for (int i = 0; i < widths.Count; i++)
            {
                _list.Columns[i].Width = widths[i];
            }
        }

        if (_config.SaveList.SortColumnIndex >= 0)
        {
            _sorter.SortColumn = _config.SaveList.SortColumnIndex;
            _sorter.Ascending = _config.SaveList.SortAscending;
        }
    }

    private void SaveWindowAndListConfig()
    {
        // RestoreBounds gives the last NORMAL (non-maximized/minimized) bounds
        // even while the window is currently maximized - exactly what we need
        // so un-maximizing later goes back to a sane size/position instead of
        // the maximized full-screen rectangle.
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

        _config.Window.X = bounds.X;
        _config.Window.Y = bounds.Y;
        _config.Window.Width = bounds.Width;
        _config.Window.Height = bounds.Height;
        _config.Window.Maximized = WindowState == FormWindowState.Maximized;

        _config.SaveList.ColumnWidths = _list.Columns.Cast<ColumnHeader>().Select(c => c.Width).ToList();
        _config.SaveList.SortColumnIndex = _sorter.SortColumn;
        _config.SaveList.SortAscending = _sorter.Ascending;

        _configService.Save(_config);
    }

    private void SortByColumn(int column)
    {
        if (_sorter.SortColumn == column)
        {
            _sorter.Ascending = !_sorter.Ascending;
        }
        else
        {
            _sorter.SortColumn = column;
            _sorter.Ascending = true;
        }
        _list.Sort();
    }

    // -------------------------------------------------------------------
    // Data loading
    // -------------------------------------------------------------------

    private void LoadData()
    {
        _config = _configService.Load();

        var liveSaves = _scanner.ScanLive(_config.SaveDir);
        var storageSaves = _scanner.ScanStorage(_config.StorageDir);
        var allSaves = liveSaves.Concat(storageSaves).ToList();

        _groups = PlaythroughGroup.GroupSaves(allSaves, _config.Nicknames);

        BuildTree();
        _statusLabel.Text = $"{liveSaves.Count} live, {storageSaves.Count} im Storage, {_groups.Count} Charakter(e). " +
                             $"Save-Dir: {_config.SaveDir} | Storage-Dir: {_config.StorageDir}";
    }

    private void BuildTree()
    {
        var previouslySelectedKey = GetSelectedGroup()?.PlaythroughKey;

        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        foreach (var group in _groups)
        {
            var label = group.IsUnknown ? "Unbekannt (keine/defekte Metadaten)" : group.DisplayLabel;
            var characterNode = new TreeNode(label) { Tag = group };

            var liveNode = new TreeNode($"Live ({group.Live.Count()})") { Tag = (group, SaveLocation.Live) };
            var storageNode = new TreeNode($"Storage ({group.Stored.Count()})") { Tag = (group, SaveLocation.Storage) };
            characterNode.Nodes.Add(liveNode);
            characterNode.Nodes.Add(storageNode);

            _tree.Nodes.Add(characterNode);

            if (group.PlaythroughKey == previouslySelectedKey)
            {
                characterNode.Expand();
                _tree.SelectedNode = characterNode;
            }
        }

        if (_tree.SelectedNode is null)
        {
            _tree.ExpandAll();
        }

        _tree.EndUpdate();
    }

    private void UpdateListForSelection()
    {
        _isLoadingList = true;
        _list.Items.Clear();
        _screenshotBox.Image = null;
        _metaLabel.Text = string.Empty;

        var node = _tree.SelectedNode;
        if (node is null)
        {
            _isLoadingList = false;
            return;
        }

        IEnumerable<SaveEntry> saves = node.Tag switch
        {
            PlaythroughGroup g => g.Saves,
            ValueTuple<PlaythroughGroup, SaveLocation> sel => sel.Item2 == SaveLocation.Live ? sel.Item1.Live : sel.Item1.Stored,
            _ => Enumerable.Empty<SaveEntry>()
        };

        foreach (var save in saves)
        {
            var row = new ListViewItem(save.SaveType.ToString()) { Tag = save };
            row.SubItems.Add(save.FolderName);
            row.SubItems.Add(save.Metadata?.Level?.ToString("0") ?? "?");
            row.SubItems.Add(Formatting.PlayTime(save.Metadata?.PlayTimeSeconds));
            row.SubItems.Add(Formatting.Timestamp(save.EffectiveTimestampUtc));
            row.SubItems.Add(save.Metadata?.TrackedQuest ?? save.Metadata?.LocationName ?? "");
            row.SubItems.Add(Formatting.Bytes(save.TotalSizeBytes));
            _list.Items.Add(row);
        }

        _isLoadingList = false;
        _list.Sort(); // applies the currently active sort column/direction (default: Zeitpunkt desc)
    }

    private void UpdatePreview()
    {
        if (_isLoadingList) return;

        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not SaveEntry save)
        {
            _screenshotBox.Image = null;
            _metaLabel.Text = string.Empty;
            return;
        }

        _screenshotBox.Image?.Dispose();
        _screenshotBox.Image = null;
        if (save.ScreenshotPath is not null && !save.ScreenshotIsPlaceholder)
        {
            try
            {
                using var stream = File.OpenRead(save.ScreenshotPath);
                _screenshotBox.Image = Image.FromStream(stream);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                // Corrupt/unreadable image - fall back to no preview rather than crashing.
                _screenshotBox.Image = null;
            }
        }

        var m = save.Metadata;
        var lines = new List<string> { $"Ordner: {save.FolderName}" };
        if (m is null)
        {
            lines.Add("");
            lines.Add("Keine Metadaten lesbar (defekt oder fehlend).");
        }
        else
        {
            lines.AddRange(new[]
            {
                $"LifePath: {m.LifePath}",
                $"Geschlecht: {m.BodyGender}",
                $"Level: {m.Level:0}",
                $"Street Cred: {m.StreetCred:0}",
                $"Spielzeit: {Formatting.PlayTime(m.PlayTimeSeconds)}",
                $"Zeitpunkt: {m.TimestampString}",
                $"Schwierigkeit: {m.Difficulty}",
                $"Quest: {m.TrackedQuest}",
                $"Ort: {m.LocationName}",
                $"Checkpoint: {m.IsCheckpoint}",
                $"Modded: {m.IsModded}",
                $"DLCs: {(m.AdditionalContentIds is null ? "-" : string.Join(", ", m.AdditionalContentIds))}",
                $"Save-Version: {m.SaveVersion} / Game-Version: {m.GameVersion} ({m.BuildPatch})",
                $"PlaythroughID: {m.PlaythroughId}",
            });
        }

        lines.Add($"Größe: {Formatting.Bytes(save.TotalSizeBytes)}");
        if (save.OldFileCount > 0)
        {
            lines.Add($"⚠ Alte Dateien (*.old): {save.OldFileCount}");
        }

        _metaLabel.Text = string.Join("\r\n", lines);
    }

    private PlaythroughGroup? GetSelectedGroup() => _tree.SelectedNode?.Tag switch
    {
        PlaythroughGroup g => g,
        ValueTuple<PlaythroughGroup, SaveLocation> sel => sel.Item1,
        _ => null
    };

    // -------------------------------------------------------------------
    // Character-level actions
    // -------------------------------------------------------------------

    private void RenameSelectedCharacter()
    {
        if (GetSelectedGroup() is not { } group || group.IsUnknown) return;

        using var dialog = new NicknameDialog("Nickname ändern", group.Nickname ?? "");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (string.IsNullOrWhiteSpace(dialog.Value))
        {
            _config.Nicknames.Remove(group.PlaythroughKey);
        }
        else
        {
            _config.Nicknames[group.PlaythroughKey] = dialog.Value;
        }

        _configService.Save(_config);
        LoadData();
    }

    private void RetireSelectedCharacter()
    {
        if (GetSelectedGroup() is not { } group || group.IsUnknown) return;

        using var dialog = new RetireDialog(group.DisplayLabel, group.Live.Count(), group.Stored.Count());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenMode is not { } mode) return;

        var result = _actionService.Retire(group, mode, _config.StorageDir, ManagedRoots);

        var verb = mode == RetirementMode.StoreAll ? "eingelagert" : "gelöscht";
        var message = $"{result.SuccessCount} von {result.Results.Count} Save(s) {verb}.";
        if (result.FailureCount > 0)
        {
            message += "\r\n\r\nFehlgeschlagen:\r\n" + string.Join("\r\n",
                result.Results.Where(r => !r.Success).Select(r => $"- {r.Save.FolderName}: {r.Error}"));
        }

        MessageBox.Show(this, message, "Charakter in Rente schicken - Ergebnis",
            MessageBoxButtons.OK,
            result.FailureCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        LoadData();
    }

    // -------------------------------------------------------------------
    // Single-save actions
    // -------------------------------------------------------------------

    private SaveEntry? GetSelectedSave() =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as SaveEntry : null;

    private void StoreSelectedSave()
    {
        if (GetSelectedSave() is not { } save || save.Location != SaveLocation.Live) return;

        var result = _actionService.StoreSave(save, _config.StorageDir);
        if (!result.Success)
        {
            MessageBox.Show(this, $"Storen fehlgeschlagen: {result.Error}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        LoadData();
    }

    private void DeleteSelectedSave()
    {
        if (GetSelectedSave() is not { } save) return;

        var confirm = MessageBox.Show(
            this,
            $"\"{save.FolderName}\" endgültig löschen? Das kann nicht rückgängig gemacht werden.",
            "Löschen?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        var result = _actionService.DeleteSave(save, ManagedRoots);
        if (!result.Success)
        {
            MessageBox.Show(this, $"Löschen fehlgeschlagen: {result.Error}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        LoadData();
    }

    // -------------------------------------------------------------------
    // Settings / cleanup
    // -------------------------------------------------------------------

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_config);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // Preserve window/list state already tracked on _config; only the
        // path/cleanup fields come from the settings dialog.
        dialog.Result.Window = _config.Window;
        dialog.Result.SaveList = _config.SaveList;

        _configService.Save(dialog.Result);
        LoadData();
    }

    private void RunCleanup(IReadOnlyList<PlaythroughGroup> targetGroups)
    {
        var allCandidates = new List<CleanupPlanItem>();
        var totalKept = 0;

        foreach (var group in targetGroups)
        {
            var plan = _planner.Plan(group.PlaythroughKey, group.Live, _config.CleanupRule);
            allCandidates.AddRange(plan.ToMove);
            totalKept += plan.KeptLive.Count;
        }

        using var preview = new CleanupPreviewForm(allCandidates, totalKept);
        if (preview.ShowDialog(this) != DialogResult.OK || preview.ConfirmedItems.Count == 0)
        {
            return;
        }

        var executionPlan = new CleanupPlan(string.Empty, preview.ConfirmedItems, Array.Empty<SaveEntry>());
        var results = _executor.Execute(executionPlan, _config.StorageDir);

        var succeeded = results.Count(r => r.MoveResult.Success);
        var failed = results.Where(r => !r.MoveResult.Success).ToList();

        var message = $"{succeeded} von {results.Count} Save(s) erfolgreich ins Storage-Dir verschoben.";
        if (failed.Count > 0)
        {
            message += "\r\n\r\nFehlgeschlagen:\r\n" + string.Join("\r\n",
                failed.Select(f => $"- {f.Item.Save.FolderName}: {f.MoveResult.Error}"));
        }

        MessageBox.Show(this, message, "Ausräumen - Ergebnis",
            MessageBoxButtons.OK,
            failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        LoadData();
    }
}
