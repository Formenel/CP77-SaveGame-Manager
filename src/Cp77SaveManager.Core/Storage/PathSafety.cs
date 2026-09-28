using System.Text.RegularExpressions;

namespace Cp77SaveManager.Core.Storage;

/// <summary>
/// Path checks shared by scanner, mover, delete and settings (review 2026-09-28, S1-S4).
/// </summary>
public static partial class PathSafety
{
    /// <summary>
    /// Real playthroughIDs are 16 lowercase hex chars (verified on 56 saves).
    /// Allowlist is a bit wider on purpose so unknown older/newer formats still
    /// group correctly - but nothing that could act as a path (.., \, /, C:).
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeIdRegex();

    // Windows device names are valid regex matches but not usable as folder names.
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex ReservedNameRegex();

    public static bool IsSafePlaythroughId(string? id) =>
        id is not null && SafeIdRegex().IsMatch(id) && !ReservedNameRegex().IsMatch(id);

    /// <summary>Full path without trailing separator; null for empty/invalid input.</summary>
    public static string? NormalizeOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>True if path is strictly inside root (textual check, case-insensitive).</summary>
    public static bool IsStrictlyUnder(string path, string root)
    {
        var p = NormalizeOrNull(path);
        var r = NormalizeOrNull(root);
        if (p is null || r is null || p.Length <= r.Length) return false;
        return p.StartsWith(r, StringComparison.OrdinalIgnoreCase)
               && (p[r.Length] == Path.DirectorySeparatorChar || r.EndsWith(Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Strictly inside root AND no directory between root (exclusive) and path
    /// (inclusive) is a reparse point (junction/symlink). The root itself may be
    /// a link - that's a legit setup (saves moved to another drive).
    /// </summary>
    public static bool IsSafelyUnder(string path, string root)
    {
        if (!IsStrictlyUnder(path, root)) return false;
        var r = NormalizeOrNull(root)!;
        var current = NormalizeOrNull(path);
        while (current is not null && current.Length > r.Length)
        {
            if (IsReparsePoint(current)) return false;
            current = Path.GetDirectoryName(current);
        }
        return true;
    }

    public static bool IsReparsePoint(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false; // doesn't exist / unreadable - nothing to follow
        }
    }

    /// <summary>
    /// Validates the two configured roots. Returns a localization key describing
    /// the problem, or null if fine. Rejects: empty, relative, drive roots,
    /// identical, or one nested inside the other (S4 - nesting let "Storen" on
    /// the storage folder wipe the whole storage).
    /// </summary>
    public static string? ValidateRoots(string? saveDir, string? storageDir)
    {
        if (string.IsNullOrWhiteSpace(saveDir) || string.IsNullOrWhiteSpace(storageDir)) return "ERR_DIR_EMPTY";
        if (!Path.IsPathFullyQualified(saveDir.Trim()) || !Path.IsPathFullyQualified(storageDir.Trim())) return "ERR_DIR_NOT_ABSOLUTE";

        var s = NormalizeOrNull(saveDir);
        var t = NormalizeOrNull(storageDir);
        if (s is null || t is null) return "ERR_DIR_NOT_ABSOLUTE";

        if (IsDriveRoot(s) || IsDriveRoot(t)) return "ERR_DIR_DRIVE_ROOT";
        if (string.Equals(s, t, StringComparison.OrdinalIgnoreCase)) return "ERR_DIR_SAME";
        if (IsStrictlyUnder(s, t) || IsStrictlyUnder(t, s)) return "ERR_DIR_NESTED";
        return null;
    }

    private static bool IsDriveRoot(string normalizedPath)
    {
        var root = Path.GetPathRoot(normalizedPath);
        return root is not null && string.Equals(Path.TrimEndingDirectorySeparator(root), normalizedPath, StringComparison.OrdinalIgnoreCase);
    }
}
