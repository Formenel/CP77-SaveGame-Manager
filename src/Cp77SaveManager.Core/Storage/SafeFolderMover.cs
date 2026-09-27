using System.Security.Cryptography;

namespace Cp77SaveManager.Core.Storage;

public sealed record MoveResult(bool Success, string DestinationPath, string? Error);

/// <summary>
/// Moves a whole save folder as an atomic unit, safe for source and
/// destination being on different drives (Werner's real setup: exe/save-dir on
/// one drive, storage on another - Directory.Move / File.Move cannot cross
/// volumes on Windows, so this always does copy -> verify -> delete instead of
/// ever assuming a same-volume rename is possible).
///
/// Safety invariants:
///  - Every file is copied to a ".partial" temp name first, never directly to
///    its final name, so a half-written destination is never mistaken for a
///    complete one if the process dies mid-copy.
///  - Every copied file is verified (size, then SHA-256) against the source
///    before anything at the source is touched.
///  - The source folder is deleted ONLY after every single file in it has
///    been verified at the destination. Any failure leaves the source
///    completely untouched and cleans up the partial destination.
/// </summary>
public sealed class SafeFolderMover
{
    public MoveResult MoveFolder(string sourceDir, string destinationParentDir, string destinationFolderName)
    {
        if (!Directory.Exists(sourceDir))
        {
            return new MoveResult(false, string.Empty, $"Quellordner existiert nicht: {sourceDir}");
        }

        var destDir = Path.Combine(destinationParentDir, destinationFolderName);
        if (Directory.Exists(destDir))
        {
            return new MoveResult(false, destDir, $"Zielordner existiert bereits: {destDir}");
        }

        Directory.CreateDirectory(destinationParentDir);

        var partialDestDir = destDir + ".partial";
        if (Directory.Exists(partialDestDir))
        {
            // Leftover from a previous crashed run - never reuse, always start clean.
            Directory.Delete(partialDestDir, recursive: true);
        }
        Directory.CreateDirectory(partialDestDir);

        // Snapshotted BEFORE anything reads the source: enumerating a
        // directory's contents, and reading a file's bytes for the SHA-256
        // verify below, both bump that item's own last-access time as a side
        // effect. Grabbing the values into plain locals up front - rather
        // than re-querying a FileInfo/DirectoryInfo later, after those reads
        // already happened - is what actually preserves the original
        // access time instead of copying our own read's timestamp.
        var sourceDirTimestamps = TimestampSnapshot.Capture(sourceDir, isDirectory: true);
        var fileSnapshots = Directory.EnumerateFiles(sourceDir)
            .ToDictionary(f => f, f => TimestampSnapshot.Capture(f, isDirectory: false));

        try
        {
            foreach (var (sourceFile, snapshot) in fileSnapshots)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destFile = Path.Combine(partialDestDir, fileName);

                File.Copy(sourceFile, destFile, overwrite: false);

                if (!FilesMatch(sourceFile, destFile))
                {
                    throw new IOException($"Verifikation fehlgeschlagen für Datei: {fileName}");
                }

                // File.Copy sets the new file's creation time to "now" (only
                // content + last-write time carry over on Windows) - restore
                // all three timestamps to match the pre-read original exactly.
                snapshot.ApplyTo(destFile, isDirectory: false);
            }

            // All files verified - promote partial dir to its real name.
            Directory.Move(partialDestDir, destDir);

            // Restore the FOLDER's own timestamps too, last (so nothing after
            // this touches it again and bumps last-write time back to "now").
            sourceDirTimestamps.ApplyTo(destDir, isDirectory: true);

            // Only now touch the source.
            Directory.Delete(sourceDir, recursive: true);

            return new MoveResult(true, destDir, null);
        }
        catch (Exception ex)
        {
            // Clean up the partial destination; source is untouched because we
            // only ever deleted it after a successful promote+verify above.
            TryDeleteDirectory(partialDestDir);
            return new MoveResult(false, destDir, ex.Message);
        }
    }

    private static bool FilesMatch(string a, string b)
    {
        var infoA = new FileInfo(a);
        var infoB = new FileInfo(b);
        if (infoA.Length != infoB.Length) return false;

        return ComputeSha256(a) == ComputeSha256(b);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// A frozen copy of a file or directory's three Windows timestamps
    /// (Created, Modified, Accessed), taken via the static File/Directory
    /// Get*TimeUtc calls rather than a FileInfo/DirectoryInfo instance -
    /// static calls always hit the OS fresh at the moment of the call and
    /// are never implicitly re-queried later, which is exactly what "freeze
    /// this before we read the file" requires.
    /// </summary>
    private readonly record struct TimestampSnapshot(DateTime CreatedUtc, DateTime LastWriteUtc, DateTime LastAccessUtc)
    {
        public static TimestampSnapshot Capture(string path, bool isDirectory) => isDirectory
            ? new TimestampSnapshot(Directory.GetCreationTimeUtc(path), Directory.GetLastWriteTimeUtc(path), Directory.GetLastAccessTimeUtc(path))
            : new TimestampSnapshot(File.GetCreationTimeUtc(path), File.GetLastWriteTimeUtc(path), File.GetLastAccessTimeUtc(path));

        /// <summary>
        /// Best-effort: some filesystems/mount types don't support setting
        /// creation time (or reject it for a directory) - swallow that
        /// specific failure rather than turning a successful, verified move
        /// into an error over a cosmetic timestamp.
        /// </summary>
        public void ApplyTo(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                try { Directory.SetCreationTimeUtc(path, CreatedUtc); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { Directory.SetLastWriteTimeUtc(path, LastWriteUtc); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { Directory.SetLastAccessTimeUtc(path, LastAccessUtc); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            else
            {
                try { File.SetCreationTimeUtc(path, CreatedUtc); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { File.SetLastWriteTimeUtc(path, LastWriteUtc); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { File.SetLastAccessTimeUtc(path, LastAccessUtc); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only - do not mask the original error with a cleanup failure.
        }
    }
}
