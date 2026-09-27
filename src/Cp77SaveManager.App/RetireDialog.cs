using Cp77SaveManager.Core.Localization;
using Cp77SaveManager.Core.Retirement;

namespace Cp77SaveManager.App;

/// <summary>Asks which of the two retirement modes to apply, or lets the user back out.</summary>
internal sealed class RetireDialog : Form
{
    public RetirementMode? ChosenMode { get; private set; }

    public RetireDialog(Translator t, string characterLabel, int liveCount, int storedCount)
    {
        Text = t.Get("DLG_RETIRE_TITLE");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 190);

        var info = new Label
        {
            Left = 12,
            Top = 12,
            Width = 396,
            Height = 60,
            Text = t.Get("RETIRE_INFO", characterLabel, liveCount, storedCount)
        };

        var storeButton = new Button { Text = t.Get("RETIRE_BTN_STORE_ALL"), Left = 12, Top = 80, Width = 396, Height = 32 };
        var deleteButton = new Button { Text = t.Get("RETIRE_BTN_DELETE_ALL"), Left = 12, Top = 118, Width = 396, Height = 32 };
        var cancelButton = new Button { Text = t.Get("BTN_CANCEL"), Left = 12, Top = 156, Width = 396, DialogResult = DialogResult.Cancel };

        storeButton.Click += (_, _) =>
        {
            ChosenMode = RetirementMode.StoreAll;
            DialogResult = DialogResult.OK;
        };
        deleteButton.Click += (_, _) =>
        {
            var confirm = MessageBox.Show(
                this,
                t.Get("RETIRE_CONFIRM_DELETE_ALL", liveCount + storedCount, characterLabel),
                t.Get("DLG_RETIRE_CONFIRM_TITLE"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            ChosenMode = RetirementMode.DeleteAll;
            DialogResult = DialogResult.OK;
        };

        CancelButton = cancelButton;

        Controls.AddRange(new Control[] { info, storeButton, deleteButton, cancelButton });
    }
}
