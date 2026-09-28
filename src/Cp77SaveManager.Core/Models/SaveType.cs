namespace Cp77SaveManager.Core.Models;

/// <summary>
/// The save-folder categories CP77 creates under the save directory.
/// Verified against a real save directory (folder names like "ManualSave-86",
/// "AutoSave-12", "QuickSave-3", "EndGameSave-0", "PointOfNoReturnSave-0").
/// </summary>
public enum SaveType
{
    ManualSave,
    AutoSave,
    QuickSave,
    EndGameSave,
    PointOfNoReturnSave,

    /// <summary>Folder name didn't match any known pattern (mod, future game update, etc.).</summary>
    Unknown
}

public static class SaveTypeExtensions
{
    /// <summary>
    /// Default set of types the cleanup rule applies to out of the box.
    /// Werner's choice: Quick + Manual are cleaned up by default; AutoSave,
    /// EndGameSave and PointOfNoReturnSave are left alone unless the user
    /// opts them in explicitly. Only affects a fresh config - an existing
    /// config.json keeps its own selectedTypes.
    /// </summary>
    public static readonly IReadOnlySet<SaveType> DefaultCleanupSelection =
        new HashSet<SaveType> { SaveType.QuickSave, SaveType.ManualSave };
}
