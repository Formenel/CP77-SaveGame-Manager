using Cp77SaveManager.Core.Models;
using Cp77SaveManager.Core.Storage;

namespace Cp77SaveManager.Core.Scanning;

public sealed class SaveScanner
{
    /// <summary>
    /// Screenshots at or below this size are treated as CDPR's known placeholder
    /// (observed real placeholder: 1004 bytes). See SaveEntry.ScreenshotIsPlaceholder.
    /// </summary>
    private const long PlaceholderScreenshotMaxBytes = 1024;

    /// <summary>
    /// Scans the live save directory (flat: one save folder per subfolder,
    /// e.g. Saved Games\CD Projekt Red\Cyberpunk 2077\ManualSave-86\...).
    /// </summary>
    public IReadOnlyList<SaveEntry> ScanLive(string liveSaveDir)
    {
        if (!Directory.Exists(liveSaveDir)) return Array.Empty<SaveEntry>();
        return RealSubdirectories(liveSaveDir)
            .Select(dir => TryReadSaveFolder(dir, SaveLocation.Live))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    /// <summary>
    /// Scans the storage directory, which is namespaced one level deeper by
    /// PlaythroughId: storageDir\&lt;playthroughId&gt;\&lt;OriginalFolderName&gt;\...
    /// A save sitting directly under storageDir (not yet namespaced, or from an
    /// older layout) is still picked up and its PTID subfolder is inferred from
    /// its own metadata once read - it isn't lost, just not yet organized.
    /// </summary>
    public IReadOnlyList<SaveEntry> ScanStorage(string storageDir)
    {
        if (!Directory.Exists(storageDir)) return Array.Empty<SaveEntry>();

        var results = new List<SaveEntry>();

        foreach (var topLevelDir in RealSubdirectories(storageDir))
        {
            // A top-level dir is either a PTID namespace folder (contains save
            // subfolders) or - if someone dropped a save folder directly under
            // storageDir - a save folder itself. Distinguish by whether its own
            // name matches a known save-folder pattern.
            var folderName = Path.GetFileName(topLevelDir);
            var (type, _) = SaveFolderNameParser.Parse(folderName);

            if (type != SaveType.Unknown)
            {
                var direct = TryReadSaveFolder(topLevelDir, SaveLocation.Storage);
                if (direct is not null) results.Add(direct);
                continue;
            }

            // Treat as a PTID namespace folder; descend one level.
            foreach (var saveDir in RealSubdirectories(topLevelDir))
            {
                var entry = TryReadSaveFolder(saveDir, SaveLocation.Storage);
                if (entry is not null) results.Add(entry);
            }
        }

        return results;
    }

    /// <summary>
    /// Subdirectories that are NOT junctions/symlinks (S2). A link below a
    /// managed root would make the app read, move or delete data somewhere
    /// else entirely, so it is ignored. The root itself may be a link.
    /// </summary>
    private static IEnumerable<string> RealSubdirectories(string dir) =>
        Directory.GetDirectories(dir).Where(d => !PathSafety.IsReparsePoint(d));

    private SaveEntry? TryReadSaveFolder(string dir, SaveLocation location)
    {
        var folderName = Path.GetFileName(dir);
        var (type, index) = SaveFolderNameParser.Parse(folderName);

        var metadataPath = Path.Combine(dir, "metadata.9.json");
        var metadata = MetadataReader.TryRead(metadataPath);

        var screenshotPath = Path.Combine(dir, "screenshot.png");
        var hasScreenshot = File.Exists(screenshotPath);
        var screenshotIsPlaceholder = false;
        long totalSize = 0;
        var oldFileCount = 0;

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var info = new FileInfo(file);
            totalSize += info.Length;
            if (hasScreenshot && string.Equals(file, screenshotPath, StringComparison.OrdinalIgnoreCase))
            {
                screenshotIsPlaceholder = info.Length <= PlaceholderScreenshotMaxBytes;
            }
            if (string.Equals(info.Extension, ".old", StringComparison.OrdinalIgnoreCase))
            {
                oldFileCount++;
            }
        }

        var dirInfo = new DirectoryInfo(dir);

        return new SaveEntry
        {
            FolderName = folderName,
            SaveType = type,
            Index = index,
            FullPath = dir,
            Location = location,
            Metadata = metadata,
            ScreenshotPath = hasScreenshot ? screenshotPath : null,
            ScreenshotIsPlaceholder = screenshotIsPlaceholder,
            TotalSizeBytes = totalSize,
            OldFileCount = oldFileCount,
            DirectoryLastWriteUtc = dirInfo.LastWriteTimeUtc
        };
    }
}
