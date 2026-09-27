using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.App;

/// <summary>
/// Sorts the save ListView by whichever column was clicked. Compares the
/// underlying SaveEntry (via ListViewItem.Tag) rather than the displayed
/// strings, so e.g. "Level" and "Zeitpunkt" sort numerically/chronologically
/// instead of alphabetically ("10" before "9").
/// </summary>
internal sealed class SaveListSorter : System.Collections.IComparer
{
    /// <summary>Default: column 4 = "Zeitpunkt", descending - matches the old fixed sort order.</summary>
    public int SortColumn { get; set; } = 4;
    public bool Ascending { get; set; } = false;

    public int Compare(object? x, object? y)
    {
        if (x is not ListViewItem itemX || y is not ListViewItem itemY) return 0;
        if (itemX.Tag is not SaveEntry a || itemY.Tag is not SaveEntry b) return 0;

        int result = SortColumn switch
        {
            0 => string.Compare(a.SaveType.ToString(), b.SaveType.ToString(), StringComparison.OrdinalIgnoreCase),
            1 => string.Compare(a.FolderName, b.FolderName, StringComparison.OrdinalIgnoreCase),
            2 => Nullable.Compare(a.Metadata?.Level, b.Metadata?.Level),
            3 => Nullable.Compare(a.Metadata?.PlayTimeSeconds, b.Metadata?.PlayTimeSeconds),
            4 => a.EffectiveTimestampUtc.CompareTo(b.EffectiveTimestampUtc),
            5 => string.Compare(a.Metadata?.TrackedQuest ?? a.Metadata?.LocationName, b.Metadata?.TrackedQuest ?? b.Metadata?.LocationName, StringComparison.OrdinalIgnoreCase),
            6 => a.TotalSizeBytes.CompareTo(b.TotalSizeBytes),
            _ => 0
        };

        return Ascending ? result : -result;
    }
}
