using Cp77SaveManager.Core.Localization;

namespace Cp77SaveManager.App;

/// <summary>Tiny modal text-input dialog (WinForms has no built-in InputBox without an extra package).</summary>
internal sealed class NicknameDialog : Form
{
    private readonly TextBox _textBox;

    public string Value => _textBox.Text.Trim();

    public NicknameDialog(Translator t, string title, string currentValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(360, 110);

        var label = new Label { Text = t.Get("RENAME_HINT"), Left = 12, Top = 12, Width = 336 };
        _textBox = new TextBox { Left = 12, Top = 36, Width = 336, Text = currentValue };

        var ok = new Button { Text = t.Get("BTN_OK"), DialogResult = DialogResult.OK, Left = 192, Width = 75, Top = 70 };
        var cancel = new Button { Text = t.Get("BTN_CANCEL"), DialogResult = DialogResult.Cancel, Left = 273, Width = 75, Top = 70 };

        AcceptButton = ok;
        CancelButton = cancel;

        Controls.AddRange(new Control[] { label, _textBox, ok, cancel });
    }
}
