using Cp77SaveManager.Core.Cleanup;

namespace Cp77SaveManager.App;

internal sealed class CleanupPreviewForm : Form
{
    private readonly ListView _list;

    /// <summary>Items the user left checked when they clicked "Verschieben" - only these get executed.</summary>
    public List<CleanupPlanItem> ConfirmedItems { get; } = new();

    public CleanupPreviewForm(IReadOnlyList<CleanupPlanItem> candidates, int keptCount)
    {
        Text = "Ausräumen - Vorschau";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(640, 420);

        var info = new Label
        {
            Text = candidates.Count == 0
                ? "Nichts zu tun - alle Saves liegen bereits innerhalb des Limits."
                : $"{candidates.Count} Save(s) würden ins Storage-Dir verschoben, {keptCount} bleiben live. Häkchen entfernen, um einzelne Saves davon auszunehmen.",
            Left = 12,
            Top = 10,
            Width = 616,
            Height = 40
        };

        _list = new ListView
        {
            Left = 12,
            Top = 55,
            Width = 616,
            Height = 310,
            View = View.Details,
            CheckBoxes = true,
            FullRowSelect = true
        };
        _list.Columns.Add("Typ", 110);
        _list.Columns.Add("Name", 130);
        _list.Columns.Add("Zeitpunkt", 140);
        _list.Columns.Add("Grund", 220);

        foreach (var item in candidates)
        {
            var row = new ListViewItem(item.Save.SaveType.ToString()) { Checked = true, Tag = item };
            row.SubItems.Add(item.Save.FolderName);
            row.SubItems.Add(Formatting.Timestamp(item.Save.EffectiveTimestampUtc));
            row.SubItems.Add(item.Reason);
            _list.Items.Add(row);
        }

        var moveButton = new Button { Text = "Verschieben", Left = 396, Top = 375, Width = 110, DialogResult = DialogResult.OK, Enabled = candidates.Count > 0 };
        var cancelButton = new Button { Text = "Abbrechen", Left = 512, Top = 375, Width = 110, DialogResult = DialogResult.Cancel };
        AcceptButton = moveButton;
        CancelButton = cancelButton;
        moveButton.Click += (_, _) =>
        {
            ConfirmedItems.AddRange(_list.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => (CleanupPlanItem)i.Tag!));
        };

        Controls.AddRange(new Control[] { info, _list, moveButton, cancelButton });
    }
}
