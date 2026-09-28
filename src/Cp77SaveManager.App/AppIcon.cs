using System.Reflection;

namespace Cp77SaveManager.App;

/// <summary>app.ico, embedded as resource (see .csproj) - for title bar and About dialog.</summary>
internal static class AppIcon
{
    public static Icon? Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{typeof(AppIcon).Namespace}.app.ico");
        return stream is null ? null : new Icon(stream);
    }
}
