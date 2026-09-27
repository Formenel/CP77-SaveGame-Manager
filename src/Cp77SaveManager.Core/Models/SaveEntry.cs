namespace Cp77SaveManager.Core.Models;

/// <summary>Where a save currently lives.</summary>
public enum SaveLocation
{
    Live,
    Storage
}

/// <summary>
/// One save folder (e.g. "ManualSave-86") with everything the UI needs:
/// its original folder name (must be preserved - Step 2 restore logic
/// depends on it), parsed metadata, screenshot info and raw size/mtime.
/// </summary>
public sealed class SaveEntry
{
    /// <summary>Original folder name exactly as on disk, e.g. "ManualSave-86". Never renamed.</summary>
    public required string FolderName { get; init; }

    public required SaveType SaveType { get; init; }

    /// <summary>Numeric suffix from the folder name, e.g. 86 for "ManualSave-86".</summary>
    public required int Index { get; init; }

    public required string FullPath { get; init; }

    public required SaveLocation Location { get; init; }

    /// <summary>Null when metadata.9.json is missing or failed to parse - save still shows up, just as "Unknown".</summary>
    public SaveMetadata? Metadata { get; init; }

    public string? ScreenshotPath { get; init; }

    /// <summary>
    /// True when screenshot.png exists but is a known placeholder size.
    /// Verified fact: several real saves (e.g. EndGameSave-0/1, some AutoSaves)
    /// ship a 1004-byte screenshot.png that is not a real thumbnail. Anything
    /// at or below that observed placeholder size is treated as "no real
    /// screenshot" so the UI shows a fallback icon instead of a broken/blank image.
    /// This threshold is a heuristic from observed data, not a documented CDPR spec -
    /// safe to adjust later if a genuine small screenshot turns up.
    /// </summary>
    public bool ScreenshotIsPlaceholder { get; init; }

    /// <summary>Combined size of all files in the save folder (sav.dat + sav.old + screenshot + metadata).</summary>
    public required long TotalSizeBytes { get; init; }

    /// <summary>
    /// Count of leftover "*.old" files in this save folder (the game itself
    /// creates e.g. "sav.old" as a backup during some save operations -
    /// verified on real saves like ManualSave-2/3/49/54). Normally 0 or 1,
    /// counted rather than assumed to be at most 1 so the UI stays correct if
    /// that ever changes.
    /// </summary>
    public required int OldFileCount { get; init; }

    /// <summary>Directory's last-write time - fallback sort key when metadata timestamp can't be parsed.</summary>
    public required DateTime DirectoryLastWriteUtc { get; init; }

    /// <summary>Group key for "per character" grouping. Empty string bucket = "Unknown / no metadata".</summary>
    public string PlaythroughKey => Metadata?.PlaythroughId ?? string.Empty;

    /// <summary>
    /// Best available timestamp for sorting/cleanup decisions: metadata's
    /// parsed timestamp if present, otherwise directory mtime.
    /// </summary>
    public DateTime EffectiveTimestampUtc => Metadata?.TryGetTimestampUtc() ?? DirectoryLastWriteUtc;

    public override string ToString() => FolderName;
}
