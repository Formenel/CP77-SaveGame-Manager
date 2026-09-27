using System.Reflection;
using Cp77SaveManager.Core.Cleanup;
using Cp77SaveManager.Core.Configuration;
using Cp77SaveManager.Core.Localization;
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

    // langs\ lives next to the exe (AppContext.BaseDirectory), not in
    // %APPDATA% like config.json - it's meant to be hand-editable, so it has
    // to be somewhere a translator can actually find and open it.
    private readonly LocalizationService _localizationService = new(Path.Combine(AppContext.BaseDirectory, "langs"));
    private Translator _t = null!;

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

    private readonly MenuStrip _menuStrip;
    private readonly ToolStripMenuItem _toolsMenu;
    private readonly ToolStripMenuItem _languageMenu;

    private readonly ToolStripButton _reloadButton;
    private readonly ToolStripButton _settingsButton;
    private readonly ToolStripButton _cleanupAllButton;
    private readonly ToolStripMenuItem _renameMenuItem;
    private readonly ToolStripMenuItem _cleanupOneMenuItem;
    private readonly ToolStripMenuItem _retireMenuItem;
    private readonly ToolStripMenuItem _storeMenuItem;
    private readonly ToolStripMenuItem _restoreMenuItem;
    private readonly ToolStripMenuItem _deleteMenuItem;

    private IReadOnlyCollection<string> ManagedRoots => new[] { _config.SaveDir, _config.StorageDir };

    public MainForm()
    {
        // Config (and with it the selected language) has to be known before
        // any UI text is built, so it's loaded here rather than only in the
        // Load-event LoadData() call further down.
        _config = _configService.Load();
        _localizationService.EnsureReferenceFileExists();
        _t = _localizationService.CreateTranslator(_config.Language);

        // ProductVersion comes from <Version> in Cp77SaveManager.App.csproj -
        // the one place to bump for a new release; build.bat reads the same
        // value to name the publish folder/zip, so there's nothing to keep in sync.
        Text = _t.Get("APP_TITLE", Application.ProductVersion);
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.Manual; // we place it ourselves from config

        using var languageIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"{typeof(MainForm).Namespace}.language-icon.png");
        var languageIcon = languageIconStream is not null ? Image.FromStream(languageIconStream) : null;

        _menuStrip = new MenuStrip();
        _toolsMenu = new ToolStripMenuItem(_t.Get("MENU_TOOLS"));
        _languageMenu = new ToolStripMenuItem(_t.Get("MENU_LANGUAGE"), languageIcon);
        _toolsMenu.DropDownItems.Add(_languageMenu);
        _menuStrip.Items.Add(_toolsMenu);
        MainMenuStrip = _menuStrip;
        BuildLanguageMenu();

        var toolStrip = new ToolStrip();
        _reloadButton = new ToolStripButton(_t.Get("BTN_RELOAD")) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        _reloadButton.Click += (_, _) => LoadData();
        _settingsButton = new ToolStripButton(_t.Get("BTN_SETTINGS")) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        _settingsButton.Click += (_, _) => OpenSettings();
        _cleanupAllButton = new ToolStripButton(_t.Get("BTN_CLEANUP_ALL")) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        _cleanupAllButton.Click += (_, _) => RunCleanup(_groups.Where(g => !g.IsUnknown).ToList());
        toolStrip.Items.AddRange(new ToolStripItem[] { _reloadButton, _settingsButton, new ToolStripSeparator(), _cleanupAllButton });

        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel(_t.Get("STATUS_READY"));
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
        _renameMenuItem = new ToolStripMenuItem(_t.Get("MENU_RENAME_ITEM"));
        _renameMenuItem.Click += (_, _) => RenameSelectedCharacter();
        _cleanupOneMenuItem = new ToolStripMenuItem(_t.Get("MENU_CLEANUP_CHAR_ITEM"));
        _cleanupOneMenuItem.Click += (_, _) =>
        {
            if (GetSelectedGroup() is { } group) RunCleanup(new[] { group });
        };
        _retireMenuItem = new ToolStripMenuItem(_t.Get("MENU_RETIRE_CHAR_ITEM"));
        _retireMenuItem.Click += (_, _) => RetireSelectedCharacter();
        _treeContextMenu.Items.AddRange(new ToolStripItem[] { _renameMenuItem, _cleanupOneMenuItem, new ToolStripSeparator(), _retireMenuItem });
        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.Node.Tag is PlaythroughGroup)
            {
                _tree.SelectedNode = e.Node;
                _treeContextMenu.Show(_tree, e.Location);
            }
        };

        _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, ListViewItemSorter = _sorter };
        foreach (var name in CurrentColumnBaseNames())
        {
            _list.Columns.Add(name);
        }
        _list.Columns[0].Width = 110;
        _list.Columns[1].Width = 110;
        _list.Columns[2].Width = 50;
        _list.Columns[3].Width = 80;
        _list.Columns[4].Width = 130;
        _list.Columns[5].Width = 160;
        _list.Columns[6].Width = 70;
        _list.SelectedIndexChanged += (_, _) => UpdatePreview();
        _list.ColumnClick += (_, e) => SortByColumn(e.Column);

        _listContextMenu = new ContextMenuStrip();
        _storeMenuItem = new ToolStripMenuItem(_t.Get("MENU_STORE_ITEM"));
        _storeMenuItem.Click += (_, _) => StoreSelectedSaves();
        _restoreMenuItem = new ToolStripMenuItem(_t.Get("MENU_RESTORE_ITEM"));
        _restoreMenuItem.Click += (_, _) => RestoreSelectedSaves();
        _deleteMenuItem = new ToolStripMenuItem(_t.Get("MENU_DELETE_ITEM"));
        _deleteMenuItem.Click += (_, _) => DeleteSelectedSaves();
        _listContextMenu.Items.AddRange(new ToolStripItem[] { _storeMenuItem, _restoreMenuItem, _deleteMenuItem });
        _list.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _list.GetItemAt(e.X, e.Y);
            if (hit is null) return;

            // OS-standard behaviour: right-clicking an item that's already
            // part of the current multi-selection keeps the whole selection
            // (so "Storen"/"Löschen" apply to all of it); right-clicking
            // outside it collapses to just the clicked item, same as
            // Windows Explorer.
            if (!hit.Selected)
            {
                foreach (ListViewItem item in _list.SelectedItems.Cast<ListViewItem>().ToList())
                {
                    item.Selected = false;
                }
                hit.Selected = true;
            }

            var selected = GetSelectedSaves();
            _storeMenuItem.Enabled = selected.Count > 0 && selected.All(s => s.Location == SaveLocation.Live);
            _restoreMenuItem.Enabled = selected.Count > 0 && selected.All(s => s.Location == SaveLocation.Storage);
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
        Controls.Add(_statusStrip);
        Controls.Add(toolStrip);
        Controls.Add(_menuStrip); // added last among the Top-docked controls so it ends up at the very top

        Load += (_, _) => LoadData();
        Shown += (_, _) => ApplyWindowConfigAndSplitters();
        FormClosing += (_, _) => SaveWindowAndListConfig();
    }

    // -------------------------------------------------------------------
    // Language
    // -------------------------------------------------------------------

    private void BuildLanguageMenu()
    {
        _languageMenu.DropDownItems.Clear();
        foreach (var lang in _localizationService.ScanAvailableLanguages())
        {
            var item = new ToolStripMenuItem($"{lang.DisplayName} ({lang.Code}, {lang.CompletionPercent}%)")
            {
                Checked = lang.Code == _config.Language
            };
            item.Click += (_, _) => SwitchLanguage(lang.Code);
            _languageMenu.DropDownItems.Add(item);
        }
    }

    private void SwitchLanguage(string code)
    {
        if (code == _config.Language) return;

        _config.Language = code;
        _t = _localizationService.CreateTranslator(code);
        _configService.Save(_config);

        RefreshLocalizedText();
        BuildLanguageMenu();
        LoadData(); // rebuilds tree/status text, which is otherwise only touched on load/reload
    }

    /// <summary>
    /// Re-applies every static piece of UI text in the current language.
    /// Anything rebuilt from data on every LoadData()/UpdatePreview() call
    /// (status line, tree labels, preview pane) picks up the new language
    /// automatically the next time those run - called explicitly right
    /// after this from SwitchLanguage.
    /// </summary>
    private void RefreshLocalizedText()
    {
        Text = _t.Get("APP_TITLE", Application.ProductVersion);

        _toolsMenu.Text = _t.Get("MENU_TOOLS");
        _languageMenu.Text = _t.Get("MENU_LANGUAGE");

        _reloadButton.Text = _t.Get("BTN_RELOAD");
        _settingsButton.Text = _t.Get("BTN_SETTINGS");
        _cleanupAllButton.Text = _t.Get("BTN_CLEANUP_ALL");

        _renameMenuItem.Text = _t.Get("MENU_RENAME_ITEM");
        _cleanupOneMenuItem.Text = _t.Get("MENU_CLEANUP_CHAR_ITEM");
        _retireMenuItem.Text = _t.Get("MENU_RETIRE_CHAR_ITEM");

        _storeMenuItem.Text = _t.Get("MENU_STORE_ITEM");
        _restoreMenuItem.Text = _t.Get("MENU_RESTORE_ITEM");
        _deleteMenuItem.Text = _t.Get("MENU_DELETE_ITEM");

        UpdateColumnHeaderArrows();
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

        _outerSplit.SplitterDistance = Math.Clamp(w.OuterSplitterDistance ?? 260, _outerSplit.Panel1MinSize, Math.Max(_outerSplit.Panel1MinSize, _outerSplit.Width - _outerSplit.Panel2MinSize));
        _innerSplit.SplitterDistance = Math.Clamp(w.InnerSplitterDistance ?? 380, _innerSplit.Panel1MinSize, Math.Max(_innerSplit.Panel1MinSize, _innerSplit.Width - _innerSplit.Panel2MinSize));

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
        UpdateColumnHeaderArrows();
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
        _config.Window.OuterSplitterDistance = _outerSplit.SplitterDistance;
        _config.Window.InnerSplitterDistance = _innerSplit.SplitterDistance;

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
        UpdateColumnHeaderArrows();
    }

    /// <summary>
    /// Shows which column is currently sorted, and in which direction, with a
    /// plain "▲"/"▼" suffix on the header text - no native Win32 header-control
    /// sort-arrow message needed (LVM_SETHEADERSORTICON etc.), just text. Also
    /// doubles as the one place column headers get their (localized) base text
    /// re-applied, so a language switch updates them too.
    /// </summary>
    private string[] CurrentColumnBaseNames() => new[]
    {
        _t.Get("COL_TYPE"), _t.Get("COL_NAME"), _t.Get("COL_LEVEL"), _t.Get("COL_PLAYTIME"),
        _t.Get("COL_TIMESTAMP"), _t.Get("COL_QUEST"), _t.Get("COL_SIZE")
    };

    private void UpdateColumnHeaderArrows()
    {
        var baseNames = CurrentColumnBaseNames();

        for (int i = 0; i < _list.Columns.Count; i++)
        {
            var baseName = i < baseNames.Length ? baseNames[i] : _list.Columns[i].Text;
            _list.Columns[i].Text = i == _sorter.SortColumn
                ? baseName + (_sorter.Ascending ? " ▲" : " ▼")
                : baseName;
        }
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
        _statusLabel.Text = _t.Get("STATUS_SUMMARY", liveSaves.Count, storageSaves.Count, _groups.Count, _config.SaveDir, _config.StorageDir);
    }

    private void BuildTree()
    {
        var previouslySelectedKey = GetSelectedGroup()?.PlaythroughKey;

        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        foreach (var group in _groups)
        {
            var label = group.IsUnknown ? _t.Get("TREE_UNKNOWN_CHARACTER") : group.DisplayLabel;
            var characterNode = new TreeNode(label) { Tag = group };

            var liveNode = new TreeNode(_t.Get("TREE_LIVE_COUNT", group.Live.Count())) { Tag = (group, SaveLocation.Live) };
            var storageNode = new TreeNode(_t.Get("TREE_STORAGE_COUNT", group.Stored.Count())) { Tag = (group, SaveLocation.Storage) };
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

        if (_list.SelectedItems.Count > 1)
        {
            // Multi-selection: the detail pane only ever shows one save's
            // metadata/screenshot, so for >1 just say how many are selected
            // and how big they are together - still useful at a glance,
            // without pretending to show details for a single one of them.
            _screenshotBox.Image?.Dispose();
            _screenshotBox.Image = null;
            var selectedSaves = GetSelectedSaves();
            var totalBytes = selectedSaves.Sum(s => s.TotalSizeBytes);
            _metaLabel.Text = _t.Get("PREVIEW_MULTI_SELECTED", selectedSaves.Count, Formatting.Bytes(totalBytes));
            return;
        }

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
        var lines = new List<string> { _t.Get("PREVIEW_FOLDER", save.FolderName) };
        if (m is null)
        {
            lines.Add("");
            lines.Add(_t.Get("PREVIEW_NO_METADATA"));
        }
        else
        {
            lines.AddRange(new[]
            {
                _t.Get("PREVIEW_LIFEPATH", m.LifePath),
                _t.Get("PREVIEW_GENDER", m.BodyGender),
                _t.Get("PREVIEW_LEVEL", m.Level?.ToString("0")),
                _t.Get("PREVIEW_STREETCRED", m.StreetCred?.ToString("0")),
                _t.Get("PREVIEW_PLAYTIME", Formatting.PlayTime(m.PlayTimeSeconds)),
                _t.Get("PREVIEW_TIMESTAMP", m.TimestampString),
                _t.Get("PREVIEW_DIFFICULTY", m.Difficulty),
                _t.Get("PREVIEW_QUEST", m.TrackedQuest),
                _t.Get("PREVIEW_LOCATION", m.LocationName),
                _t.Get("PREVIEW_CHECKPOINT", m.IsCheckpoint),
                _t.Get("PREVIEW_MODDED", m.IsModded),
                _t.Get("PREVIEW_DLCS", m.AdditionalContentIds is null ? _t.Get("PREVIEW_DLCS_NONE") : string.Join(", ", m.AdditionalContentIds)),
                _t.Get("PREVIEW_VERSIONS", m.SaveVersion, m.GameVersion, m.BuildPatch),
                _t.Get("PREVIEW_PLAYTHROUGH_ID", m.PlaythroughId),
            });
        }

        lines.Add(_t.Get("PREVIEW_SIZE", Formatting.Bytes(save.TotalSizeBytes)));
        if (save.OldFileCount > 0)
        {
            lines.Add(_t.Get("PREVIEW_OLD_FILES_WARNING", save.OldFileCount));
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

        using var dialog = new NicknameDialog(_t, _t.Get("DLG_RENAME_TITLE"), group.Nickname ?? "");
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

        using var dialog = new RetireDialog(_t, group.DisplayLabel, group.Live.Count(), group.Stored.Count());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ChosenMode is not { } mode) return;

        var result = _actionService.Retire(group, mode, _config.StorageDir, ManagedRoots);

        var verb = mode == RetirementMode.StoreAll ? _t.Get("RETIRE_VERB_STORED") : _t.Get("RETIRE_VERB_DELETED");
        var message = _t.Get("RESULT_COUNT_VERB", result.SuccessCount, result.Results.Count, verb);
        if (result.FailureCount > 0)
        {
            message += "\r\n\r\n" + _t.Get("RESULT_FAILURES_HEADER") + "\r\n" + string.Join("\r\n",
                result.Results.Where(r => !r.Success).Select(r => $"- {r.Save.FolderName}: {r.Error}"));
        }

        MessageBox.Show(this, message, _t.Get("DLG_RETIRE_RESULT_TITLE"),
            MessageBoxButtons.OK,
            result.FailureCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        LoadData();
    }

    // -------------------------------------------------------------------
    // Save actions (Store/Restore/Delete) - operate on the whole current
    // multi-selection, same as an OS file manager would.
    // -------------------------------------------------------------------

    private IReadOnlyList<SaveEntry> GetSelectedSaves() =>
        _list.SelectedItems.Cast<ListViewItem>().Select(i => (SaveEntry)i.Tag!).ToList();

    private void StoreSelectedSaves()
    {
        var saves = GetSelectedSaves().Where(s => s.Location == SaveLocation.Live).ToList();
        if (saves.Count == 0) return;

        var failures = new List<(SaveEntry Save, string? Error)>();
        foreach (var save in saves)
        {
            var result = _actionService.StoreSave(save, _config.StorageDir);
            if (!result.Success) failures.Add((save, result.Error));
        }

        if (failures.Count > 0)
        {
            var message = _t.Get("RESULT_STORE_SUCCESS", saves.Count - failures.Count, saves.Count) +
                          "\r\n\r\n" + _t.Get("RESULT_FAILURES_HEADER") + "\r\n" +
                          string.Join("\r\n", failures.Select(f => $"- {f.Save.FolderName}: {f.Error}"));
            MessageBox.Show(this, message, _t.Get("DLG_STORE_RESULT_TITLE"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        LoadData();
    }

    private void RestoreSelectedSaves()
    {
        var saves = GetSelectedSaves().Where(s => s.Location == SaveLocation.Storage).ToList();
        if (saves.Count == 0) return;

        var failures = new List<(SaveEntry Save, string? Error)>();
        var renamed = new List<(SaveEntry Save, string NewName)>();
        foreach (var save in saves)
        {
            var result = _actionService.RestoreSave(save, _config.SaveDir);
            if (!result.Success)
            {
                failures.Add((save, result.Error));
            }
            else if (Path.GetFileName(result.DestinationPath) != save.FolderName)
            {
                // Live slot-name collision - original name was already taken
                // by a different save, so it landed under a deduped name.
                renamed.Add((save, Path.GetFileName(result.DestinationPath)));
            }
        }

        if (failures.Count > 0 || renamed.Count > 0)
        {
            var lines = new List<string> { _t.Get("RESULT_RESTORE_SUCCESS", saves.Count - failures.Count, saves.Count) };
            if (renamed.Count > 0)
            {
                lines.Add("");
                lines.Add(_t.Get("RESULT_RESTORE_RENAMED_HEADER"));
                lines.AddRange(renamed.Select(r => $"- \"{r.Save.FolderName}\" -> \"{r.NewName}\""));
            }
            if (failures.Count > 0)
            {
                lines.Add("");
                lines.Add(_t.Get("RESULT_FAILURES_HEADER"));
                lines.AddRange(failures.Select(f => $"- {f.Save.FolderName}: {f.Error}"));
            }
            MessageBox.Show(this, string.Join("\r\n", lines), _t.Get("DLG_RESTORE_RESULT_TITLE"),
                MessageBoxButtons.OK,
                failures.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        LoadData();
    }

    private void DeleteSelectedSaves()
    {
        var saves = GetSelectedSaves();
        if (saves.Count == 0) return;

        var confirmText = saves.Count == 1
            ? _t.Get("CONFIRM_DELETE_SINGLE", saves[0].FolderName)
            : _t.Get("CONFIRM_DELETE_MULTI", saves.Count, string.Join("\r\n", saves.Select(s => $"- {s.FolderName}")));

        var confirm = MessageBox.Show(
            this,
            confirmText,
            _t.Get("DLG_DELETE_CONFIRM_TITLE"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        var failures = new List<(SaveEntry Save, string? Error)>();
        foreach (var save in saves)
        {
            var result = _actionService.DeleteSave(save, ManagedRoots);
            if (!result.Success) failures.Add((save, result.Error));
        }

        if (failures.Count > 0)
        {
            var message = _t.Get("RESULT_DELETE_SUCCESS", saves.Count - failures.Count, saves.Count) +
                          "\r\n\r\n" + _t.Get("RESULT_FAILURES_HEADER") + "\r\n" +
                          string.Join("\r\n", failures.Select(f => $"- {f.Save.FolderName}: {f.Error}"));
            MessageBox.Show(this, message, _t.Get("DLG_DELETE_RESULT_TITLE"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        LoadData();
    }

    // -------------------------------------------------------------------
    // Settings / cleanup
    // -------------------------------------------------------------------

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_t, _config);
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

        using var preview = new CleanupPreviewForm(_t, allCandidates, totalKept);
        if (preview.ShowDialog(this) != DialogResult.OK || preview.ConfirmedItems.Count == 0)
        {
            return;
        }

        var executionPlan = new CleanupPlan(string.Empty, preview.ConfirmedItems, Array.Empty<SaveEntry>());
        var results = _executor.Execute(executionPlan, _config.StorageDir);

        var succeeded = results.Count(r => r.MoveResult.Success);
        var failed = results.Where(r => !r.MoveResult.Success).ToList();

        var message = _t.Get("RESULT_CLEANUP_SUCCESS", succeeded, results.Count);
        if (failed.Count > 0)
        {
            message += "\r\n\r\n" + _t.Get("RESULT_FAILURES_HEADER") + "\r\n" + string.Join("\r\n",
                failed.Select(f => $"- {f.Item.Save.FolderName}: {f.MoveResult.Error}"));
        }

        MessageBox.Show(this, message, _t.Get("DLG_CLEANUP_RESULT_TITLE"),
            MessageBoxButtons.OK,
            failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        LoadData();
    }
}
