using Cp77SaveManager.Core.Retirement;

namespace Cp77SaveManager.App;

/// <summary>Asks which of the two retirement modes to apply, or lets the user back out.</summary>
internal sealed class RetireDialog : Form
{
    public RetirementMode? ChosenMode { get; private set; }

    public RetireDialog(string characterLabel, int liveCount, int storedCount)
    {
        Text = "Charakter in Rente schicken";
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
            Text = $"\"{characterLabel}\" hat {liveCount} Save(s) live und {storedCount} im Storage.\r\n" +
                   "Was soll passieren?"
        };

        var storeButton = new Button { Text = "Alle live Saves einlagern (Storage)", Left = 12, Top = 80, Width = 396, Height = 32 };
        var deleteButton = new Button { Text = "ALLES löschen (live + Storage) - unwiderruflich", Left = 12, Top = 118, Width = 396, Height = 32 };
        var cancelButton = new Button { Text = "Abbrechen", Left = 12, Top = 156, Width = 396, DialogResult = DialogResult.Cancel };

        storeButton.Click += (_, _) =>
        {
            ChosenMode = RetirementMode.StoreAll;
            DialogResult = DialogResult.OK;
        };
        deleteButton.Click += (_, _) =>
        {
            var confirm = MessageBox.Show(
                this,
                $"Wirklich ALLE {liveCount + storedCount} Save(s) von \"{characterLabel}\" endgültig löschen?\r\nDas kann nicht rückgängig gemacht werden.",
                "Endgültig löschen?",
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
