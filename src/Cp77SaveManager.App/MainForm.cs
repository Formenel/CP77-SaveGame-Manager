using System.Reflection;
using Cp77SaveManager.Core.Cleanup;
using Cp77SaveManager.Core.Configuration;
using Cp77SaveManager.Core.Localization;
using Cp77SaveManager.Core.Models;
using Cp77SaveManager.Core.Retirement;
using Cp77SaveManager.Core.Scanning;
using Cp77SaveManager.Core.Storage;

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
    private readonly ToolStripMenuItem _languageMenu;

    /// <summary>
    /// Every menu entry / toolbar button, registered once: text key for
    /// language switches, optional shortcut text key, and when it's enabled.
    /// Main menu, toolbar and both context menus are built from the same
    /// actions, so their texts and enabled states can't drift apart.
    /// </summary>
    private readonly List<(ToolStripItem Item, string TextKey, string? ShortcutTextKey, Func<bool> CanRun)> _commands = new();

    /// <summary>
    /// "CP77 Save Manager 0.3.0" - ProductVersion is the informational version,
    /// which the SDK suffixes with "+&lt;git commit hash&gt;"; only the part
    /// before "+" is the plain &lt;Version&gt; from the csproj. Not translated.
    /// </summary>
    private static string AppTitle => $"CP77 Save Manager {Application.ProductVersion.Split('+')[0]}";

    private IReadOnlyCollection<string> ManagedRoots => new[] { _config.SaveDir, _config.StorageDir };

    public MainForm()
    {
        // Config (and with it the selected language) has to be known before
        // any UI text is built, so it's loaded here rather than only in the
        // Load-event LoadData() call further down.
        _config = _configService.Load();
        _localizationService.EnsureReferenceFileExists();
        _t = _localizationService.CreateTranslator(_config.Language);
        CoreText.Current = _t;

        // ProductVersion comes from <Version> in Cp77SaveManager.App.csproj -
        // the one place to bump for a new release; build.bat reads the same
        // value to name the publish folder/zip, so there's nothing to keep in sync.
        Text = AppTitle;
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.Manual; // we place it ourselves from config

        // --- actions (shared by menu, toolbar, context menus) ---------------
        Func<bool> always = () => true;
        Func<bool> canStore = () => GetSelectedSaves() is { Count: > 0 } s && s.All(x => x.Location == SaveLocation.Live);
        Func<bool> canRestore = () => GetSelectedSaves() is { Count: > 0 } s && s.All(x => x.Location == SaveLocation.Storage);
        Func<bool> hasSelection = HasSelection;
        Func<bool> hasItems = HasItems;
        Func<bool> isCharacter = () => GetSelectedGroup() is { IsUnknown: false }; // "Unknown" isn't a character
        Func<bool> anyCharacter = () => _groups.Any(g => !g.IsUnknown);

        Action cleanupOne = () => { if (GetSelectedGroup() is { IsUnknown: false } group) RunCleanup(new[] { group }); };
        Action cleanupAll = () => RunCleanup(_groups.Where(g => !g.IsUnknown).ToList());

        // --- main menu ------------------------------------------------------
        _menuStrip = new MenuStrip();

        var fileMenu = TopMenu("MENU_FILE",
            MenuItem("BTN_RELOAD", LoadData, always, Keys.F5, "SHORTCUT_RELOAD"),
            new ToolStripSeparator(),
            MenuItem("MENU_EXIT", Close, always));

        var savesMenu = TopMenu("MENU_SAVES",
            MenuItem("MENU_STORE_ITEM", StoreSelectedSaves, canStore),
            MenuItem("MENU_RESTORE_ITEM", RestoreSelectedSaves, canRestore),
            MenuItem("MENU_DELETE_ITEM", DeleteSelectedSaves, hasSelection),
            new ToolStripSeparator(),
            MenuItem("MENU_SELECT_ALL", SelectAllSaves, hasItems, Keys.Control | Keys.A, "MENU_SELECT_ALL_SHORTCUT"));

        var characterMenu = TopMenu("MENU_CHARACTER",
            MenuItem("MENU_RENAME_ITEM", RenameSelectedCharacter, isCharacter, Keys.F2, "SHORTCUT_RENAME"),
            MenuItem("MENU_CLEANUP_CHAR_ITEM", cleanupOne, isCharacter),
            MenuItem("BTN_CLEANUP_ALL", cleanupAll, anyCharacter),
            new ToolStripSeparator(),
            MenuItem("MENU_RETIRE_CHAR_ITEM", RetireSelectedCharacter, isCharacter));

        using var languageIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"{typeof(MainForm).Namespace}.language-icon.png");
        var languageIcon = languageIconStream is not null ? Image.FromStream(languageIconStream) : null;
        _languageMenu = new ToolStripMenuItem(_t.Get("MENU_LANGUAGE"), languageIcon);
        _commands.Add((_languageMenu, "MENU_LANGUAGE", null, always));
        var toolsMenu = TopMenu("MENU_TOOLS",
            MenuItem("BTN_SETTINGS", OpenSettings, always),
            _languageMenu);

        var helpMenu = TopMenu("MENU_HELP",
            MenuItem("MENU_ABOUT", ShowAbout, always));

        _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, savesMenu, characterMenu, toolsMenu, helpMenu });
        MainMenuStrip = _menuStrip;
        BuildLanguageMenu();

        // --- toolbar: only the frequent actions, no (useless) drag grip -----
        var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        toolStrip.Items.AddRange(new ToolStripItem[]
        {
            ToolButton("BTN_RELOAD", LoadData, always),
            new ToolStripSeparator(),
            ToolButton("BTN_STORE", StoreSelectedSaves, canStore),
            ToolButton("BTN_RESTORE", RestoreSelectedSaves, canRestore),
            new ToolStripSeparator(),
            ToolButton("BTN_CLEANUP_ALL", cleanupAll, anyCharacter)
        });

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
        _tree.AfterSelect += (_, _) =>
        {
            UpdateListForSelection();
            UpdateCommandStates();
        };

        // Context menus repeat the main menu entries (without shortcut keys -
        // those are handled once, by the main menu).
        _treeContextMenu = new ContextMenuStrip();
        _treeContextMenu.Items.AddRange(new ToolStripItem[]
        {
            MenuItem("MENU_RENAME_ITEM", RenameSelectedCharacter, isCharacter, shortcutTextKey: "SHORTCUT_RENAME"),
            MenuItem("MENU_CLEANUP_CHAR_ITEM", cleanupOne, isCharacter),
            new ToolStripSeparator(),
            MenuItem("MENU_RETIRE_CHAR_ITEM", RetireSelectedCharacter, isCharacter)
        });
        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.Node.Tag is PlaythroughGroup)
            {
                _tree.SelectedNode = e.Node;
                UpdateCommandStates();
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
        _list.SelectedIndexChanged += (_, _) =>
        {
            UpdatePreview();
            if (!_isLoadingList) UpdateCommandStates();
        };
        _list.ColumnClick += (_, e) => SortByColumn(e.Column);

        _listContextMenu = new ContextMenuStrip();
        _listContextMenu.Items.AddRange(new ToolStripItem[]
        {
            MenuItem("MENU_STORE_ITEM", StoreSelectedSaves, canStore),
            MenuItem("MENU_RESTORE_ITEM", RestoreSelectedSaves, canRestore),
            MenuItem("MENU_DELETE_ITEM", DeleteSelectedSaves, hasSelection),
            new ToolStripSeparator(),
            MenuItem("MENU_SELECT_ALL", SelectAllSaves, hasItems, shortcutTextKey: "MENU_SELECT_ALL_SHORTCUT")
        });

        _list.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            if (_list.Items.Count == 0) return;
            var hit = _list.GetItemAt(e.X, e.Y); // null = empty area below the rows: keep selection, menu still offers "Select all"

            // OS-standard behaviour: right-clicking an item that's already
            // part of the current multi-selection keeps the whole selection
            // (so "Storen"/"Löschen" apply to all of it); right-clicking
            // outside it collapses to just the clicked item, same as
            // Windows Explorer.
            if (hit is not null && !hit.Selected)
            {
                foreach (ListViewItem item in _list.SelectedItems.Cast<ListViewItem>().ToList())
                {
                    item.Selected = false;
                }
                hit.Selected = true;
            }

            UpdateCommandStates();
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

        // Title bar / taskbar icon: <ApplicationIcon> only sets the exe file's
        // icon, the form itself would show the WinForms default otherwise.
        if (AppIcon.Load() is { } appIcon) Icon = appIcon;

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
        CoreText.Current = _t;
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
        Text = AppTitle;

        foreach (var (item, textKey, shortcutTextKey, _) in _commands)
        {
            item.Text = _t.Get(textKey);
            if (shortcutTextKey is not null && item is ToolStripMenuItem menuItem)
            {
                menuItem.ShortcutKeyDisplayString = _t.Get(shortcutTextKey);
            }
        }

        UpdateColumnHeaderArrows();
    }

    // -------------------------------------------------------------------
    // Menu / toolbar plumbing
    // -------------------------------------------------------------------

    private ToolStripMenuItem TopMenu(string textKey, params ToolStripItem[] items)
    {
        var menu = new ToolStripMenuItem(_t.Get(textKey));
        menu.DropDownItems.AddRange(items);
        _commands.Add((menu, textKey, null, () => true));
        return menu;
    }

    private ToolStripMenuItem MenuItem(string textKey, Action run, Func<bool> canRun, Keys shortcut = Keys.None, string? shortcutTextKey = null)
    {
        var item = new ToolStripMenuItem(_t.Get(textKey)) { ShortcutKeys = shortcut };
        if (shortcutTextKey is not null) item.ShortcutKeyDisplayString = _t.Get(shortcutTextKey);
        item.Click += (_, _) => run();
        _commands.Add((item, textKey, shortcutTextKey, canRun));
        return item;
    }

    private ToolStripButton ToolButton(string textKey, Action run, Func<bool> canRun)
    {
        var button = new ToolStripButton(_t.Get(textKey)) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += (_, _) => run();
        _commands.Add((button, textKey, null, canRun));
        return button;
    }

    /// <summary>
    /// Enables/disables every registered entry for the current tree/list
    /// selection. Also decides whether a shortcut (F2, Ctrl+A) fires -
    /// WinForms ignores shortcuts of disabled menu items.
    /// </summary>
    private bool HasSelection() => _list.SelectedItems.Count > 0;

    private bool HasItems() => _list.Items.Count > 0;

    private void UpdateCommandStates()
    {
        foreach (var (item, _, _, canRun) in _commands)
        {
            item.Enabled = canRun();
        }
    }

    private void ShowAbout()
    {
        using var dialog = new AboutDialog(_t, AppTitle, Icon);
        dialog.ShowDialog(this);
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

        // S4: never scan (and thus never offer store/delete) with an empty,
        // identical or nested save/storage dir - e.g. from a hand-edited config.json.
        var dirError = PathSafety.ValidateRoots(_config.SaveDir, _config.StorageDir);
        if (dirError is not null)
        {
            _groups = Array.Empty<PlaythroughGroup>();
            BuildTree();
            UpdateCommandStates();
            _statusLabel.Text = _t.Get("STATUS_INVALID_DIRS", _t.Get(dirError));
            return;
        }

        var liveSaves = _scanner.ScanLive(_config.SaveDir);
        var storageSaves = _scanner.ScanStorage(_config.StorageDir);
        var allSaves = liveSaves.Concat(storageSaves).ToList();

        _groups = PlaythroughGroup.GroupSaves(allSaves, _config.Nicknames);

        BuildTree();
        UpdateCommandStates();
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
                // R2: Image.FromStream needs its stream alive for the image's
                // whole lifetime - copy into a standalone Bitmap instead.
                using var stream = File.OpenRead(save.ScreenshotPath);
                using var loaded = Image.FromStream(stream);
                _screenshotBox.Image = new Bitmap(loaded);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or System.Runtime.InteropServices.ExternalException)
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

        Cursor.Current = Cursors.WaitCursor;
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

    private void SelectAllSaves()
    {
        // Guard + one preview update at the end instead of one per item.
        _isLoadingList = true;
        _list.BeginUpdate();
        foreach (ListViewItem item in _list.Items)
        {
            item.Selected = true;
        }
        _list.EndUpdate();
        _isLoadingList = false;
        UpdatePreview();
        UpdateCommandStates();
    }

    private IReadOnlyList<SaveEntry> GetSelectedSaves() =>
        _list.SelectedItems.Cast<ListViewItem>().Select(i => (SaveEntry)i.Tag!).ToList();

    private void StoreSelectedSaves()
    {
        var saves = GetSelectedSaves().Where(s => s.Location == SaveLocation.Live).ToList();
        if (saves.Count == 0) return;

        Cursor.Current = Cursors.WaitCursor; // resets itself once the UI processes messages again
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
        Cursor.Current = Cursors.WaitCursor;
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

        DialogResult confirm;
        if (saves.Count == 1)
        {
            confirm = MessageBox.Show(
                this,
                _t.Get("CONFIRM_DELETE_SINGLE", saves[0].FolderName),
                _t.Get("DLG_DELETE_CONFIRM_TITLE"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
        }
        else
        {
            // Multi: scrollable list instead of a MessageBox that grows off-screen.
            // {1} (the old inline name list) is left empty - the names go into the list.
            var question = _t.Get("CONFIRM_DELETE_MULTI", saves.Count, "").TrimEnd();
            using var dialog = new ConfirmListDialog(_t, _t.Get("DLG_DELETE_CONFIRM_TITLE"), question, saves.Select(s => s.FolderName));
            confirm = dialog.ShowDialog(this);
        }
        if (confirm != DialogResult.Yes) return;

        Cursor.Current = Cursors.WaitCursor;
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
        Cursor.Current = Cursors.WaitCursor;
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
