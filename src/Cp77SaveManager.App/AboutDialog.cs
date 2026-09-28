using System.Diagnostics;
using Cp77SaveManager.Core.Localization;

namespace Cp77SaveManager.App;

/// <summary>"?" → "Info...": icon, name + version, short description, links.</summary>
internal sealed class AboutDialog : Form
{
    private const string NexusUrl = "https://www.nexusmods.com/cyberpunk2077/mods/34408";
    private const string GitHubUrl = "https://github.com/Formenel/CP77-SaveGame-Manager";

    public AboutDialog(Translator t, string appTitle, Icon? appIcon)
    {
        Text = t.Get("DLG_ABOUT_TITLE");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 170);

        var picture = new PictureBox
        {
            Left = 16, Top = 16, Width = 64, Height = 64,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = appIcon is null ? null : new Icon(appIcon, 64, 64).ToBitmap() // picks the 64px frame of app.ico
        };
        var title = new Label { Text = appTitle, Left = 96, Top = 16, AutoSize = true, Font = new Font(Font.FontFamily, 12f, FontStyle.Bold) };
        var description = new Label { Text = t.Get("ABOUT_DESCRIPTION"), Left = 96, Top = 46, Width = 308, Height = 36 };
        var nexus = Link("Nexus Mods", NexusUrl, 96, 88);
        var github = Link("GitHub", GitHubUrl, 96, 110);
        var ok = new Button { Text = t.Get("BTN_OK"), DialogResult = DialogResult.OK, Left = 329, Top = 132, Width = 75 };
        AcceptButton = ok;
        CancelButton = ok;

        Controls.AddRange(new Control[] { picture, title, description, nexus, github, ok });
    }

    private static LinkLabel Link(string text, string url, int left, int top)
    {
        var link = new LinkLabel { Text = text, Left = left, Top = top, AutoSize = true };
        // Opens the fixed URL above in the default browser - nothing else is ever started.
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return link;
    }
}
