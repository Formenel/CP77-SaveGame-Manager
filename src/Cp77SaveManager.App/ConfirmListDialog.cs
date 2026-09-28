using Cp77SaveManager.Core.Localization;

namespace Cp77SaveManager.App;

/// <summary>
/// Yes/No confirmation with a scrollable list of items - MessageBox can't
/// scroll, so a long selection (e.g. deleting 50 saves) would run off-screen.
/// "No" is the default button, same as the MessageBox it replaces.
/// </summary>
internal sealed class ConfirmListDialog : Form
{
    public ConfirmListDialog(Translator t, string title, string question, IEnumerable<string> items)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 320);

        var icon = new PictureBox { Image = SystemIcons.Warning.ToBitmap(), Left = 12, Top = 12, Width = 32, Height = 32 };
        var label = new Label { Text = question, Left = 54, Top = 12, Width = 354, Height = 48 };
        var list = new ListBox { Left = 12, Top = 66, Width = 396, Height = 204, IntegralHeight = false, SelectionMode = SelectionMode.None };
        list.Items.AddRange(items.Cast<object>().ToArray());

        var yes = new Button { Text = t.Get("BTN_YES"), DialogResult = DialogResult.Yes, Left = 252, Width = 75, Top = 282 };
        var no = new Button { Text = t.Get("BTN_NO"), DialogResult = DialogResult.No, Left = 333, Width = 75, Top = 282 };
        AcceptButton = no;
        CancelButton = no;
        ActiveControl = no;

        Controls.AddRange(new Control[] { icon, label, list, yes, no });
    }
}
