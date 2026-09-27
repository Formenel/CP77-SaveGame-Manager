using Cp77SaveManager.Core.Configuration;
using Cp77SaveManager.Core.Localization;
using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.App;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _saveDirBox;
    private readonly TextBox _storageDirBox;
    private readonly CheckedListBox _typesList;
    private readonly NumericUpDown _keepTotalBox;

    public AppConfig Result { get; }

    public SettingsForm(Translator t, AppConfig current)
    {
        // Work on a copy so Cancel never mutates the caller's live config.
        Result = new AppConfig
        {
            SaveDir = current.SaveDir,
            StorageDir = current.StorageDir,
            Nicknames = new Dictionary<string, string>(current.Nicknames),
            CleanupRule = new CleanupRuleConfig
            {
                SelectedTypes = new List<SaveType>(current.CleanupRule.SelectedTypes),
                KeepTotal = current.CleanupRule.KeepTotal
            },
            Language = current.Language
        };

        Text = t.Get("DLG_SETTINGS_TITLE");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(460, 340);

        var saveDirLabel = new Label { Text = t.Get("SETTINGS_SAVEDIR_LABEL"), Left = 12, Top = 15, Width = 420 };
        _saveDirBox = new TextBox { Left = 12, Top = 35, Width = 340, Text = Result.SaveDir };
        var saveDirBrowse = new Button { Text = t.Get("SETTINGS_BROWSE"), Left = 358, Top = 34, Width = 30 };
        saveDirBrowse.Click += (_, _) => BrowseFolder(_saveDirBox);

        var storageDirLabel = new Label { Text = t.Get("SETTINGS_STORAGEDIR_LABEL"), Left = 12, Top = 70, Width = 420 };
        _storageDirBox = new TextBox { Left = 12, Top = 90, Width = 340, Text = Result.StorageDir };
        var storageDirBrowse = new Button { Text = t.Get("SETTINGS_BROWSE"), Left = 358, Top = 89, Width = 30 };
        storageDirBrowse.Click += (_, _) => BrowseFolder(_storageDirBox);

        var configHint = new Label
        {
            Text = t.Get("SETTINGS_CONFIG_HINT", DefaultPaths.ConfigFilePath),
            Left = 12,
            Top = 122,
            Width = 420,
            Height = 32,
            ForeColor = SystemColors.GrayText
        };

        var cleanupLabel = new Label { Text = t.Get("SETTINGS_CLEANUP_LABEL"), Left = 12, Top = 165, Width = 420 };
        _typesList = new CheckedListBox { Left = 12, Top = 185, Width = 420, Height = 90, CheckOnClick = true };
        foreach (var type in Enum.GetValues<SaveType>().Where(t2 => t2 != SaveType.Unknown))
        {
            _typesList.Items.Add(type, Result.CleanupRule.SelectedTypes.Contains(type));
        }

        var keepLabel = new Label { Text = t.Get("SETTINGS_KEEP_LABEL"), Left = 12, Top = 285, Width = 220 };
        _keepTotalBox = new NumericUpDown { Left = 240, Top = 283, Width = 60, Minimum = 0, Maximum = 9999, Value = Result.CleanupRule.KeepTotal };

        var ok = new Button { Text = t.Get("BTN_SAVE"), DialogResult = DialogResult.OK, Left = 272, Width = 85, Top = 305 };
        var cancel = new Button { Text = t.Get("BTN_CANCEL"), DialogResult = DialogResult.Cancel, Left = 362, Width = 85, Top = 305 };
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, _) => ApplyToResult();

        Controls.AddRange(new Control[]
        {
            saveDirLabel, _saveDirBox, saveDirBrowse,
            storageDirLabel, _storageDirBox, storageDirBrowse,
            configHint,
            cleanupLabel, _typesList,
            keepLabel, _keepTotalBox,
            ok, cancel
        });
    }

    private static void BrowseFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(target.Text) ? target.Text : "" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private void ApplyToResult()
    {
        Result.SaveDir = _saveDirBox.Text.Trim();
        Result.StorageDir = _storageDirBox.Text.Trim();
        Result.CleanupRule.SelectedTypes = _typesList.CheckedItems.Cast<SaveType>().ToList();
        Result.CleanupRule.KeepTotal = (int)_keepTotalBox.Value;
    }
}
