namespace Cp77SaveManager.App;

internal static class Formatting
{
    public static string Bytes(long bytes)
    {
        double mb = bytes / 1024.0 / 1024.0;
        return mb >= 1 ? $"{mb:0.0} MB" : $"{bytes / 1024.0:0.0} KB";
    }

    public static string PlayTime(double? seconds)
    {
        if (seconds is null) return "?";
        var span = TimeSpan.FromSeconds(seconds.Value);
        return $"{(int)span.TotalHours}h {span.Minutes:00}m";
    }

    public static string Timestamp(DateTime utc) => utc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}
