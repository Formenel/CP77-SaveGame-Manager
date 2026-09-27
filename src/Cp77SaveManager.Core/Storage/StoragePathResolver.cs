using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.Core.Storage;

/// <summary>
/// Decides where in the storage dir a save folder ends up:
/// storageDir\&lt;playthroughId&gt;\&lt;OriginalFolderName&gt;\
/// (or storageDir\_unknown\&lt;OriginalFolderName&gt;\ when metadata is missing).
///
/// The original folder name (e.g. "ManualSave-86") is always kept as-is per
/// Werner's requirement, since Step 2 (restore) will need it. Because the
/// live save dir reuses slot numbers across characters over time, the SAME
/// original folder name can end up in storage twice for the same character
/// (e.g. "ManualSave-12" archived once, then the live game reuses slot 12 for
/// the same character later, and that gets archived too). This resolver
/// detects that collision and appends the save's own timestamp to keep both
/// copies instead of silently overwriting one.
/// </summary>
public sealed class StoragePathResolver
{
    private const string UnknownPlaythroughFolderName = "_unknown";

    public string ResolveDestinationParent(string storageDir, SaveEntry save)
    {
        var ptidFolder = string.IsNullOrEmpty(save.PlaythroughKey)
            ? UnknownPlaythroughFolderName
            : save.PlaythroughKey;

        return Path.Combine(storageDir, ptidFolder);
    }

    /// <summary>
    /// Returns a destination folder name that won't collide with anything
    /// already present under destinationParent. Prefers the original folder
    /// name unchanged; only appends a timestamp suffix if that exact name is
    /// already taken.
    /// </summary>
    public string ResolveDestinationFolderName(string destinationParent, SaveEntry save)
    {
        var candidate = save.FolderName;
        if (!Directory.Exists(Path.Combine(destinationParent, candidate)))
        {
            return candidate;
        }

        var suffix = save.EffectiveTimestampUtc.ToString("yyyyMMdd-HHmmss");
        var deduped = $"{save.FolderName}__{suffix}";

        // Extremely unlikely second collision (same second, same slot) - fall
        // back to an incrementing counter rather than looping forever.
        var counter = 2;
        while (Directory.Exists(Path.Combine(destinationParent, deduped)))
        {
            deduped = $"{save.FolderName}__{suffix}-{counter}";
            counter++;
        }

        return deduped;
    }
}
