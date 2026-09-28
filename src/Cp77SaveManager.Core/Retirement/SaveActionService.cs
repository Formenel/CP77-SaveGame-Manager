using Cp77SaveManager.Core.Localization;
using Cp77SaveManager.Core.Models;
using Cp77SaveManager.Core.Storage;

namespace Cp77SaveManager.Core.Retirement;

public enum RetirementMode
{
    /// <summary>Move every remaining live save of the character to storage. Nothing is deleted.</summary>
    StoreAll,

    /// <summary>Permanently delete every save of the character, live AND storage. Irreversible.</summary>
    DeleteAll
}

public sealed record SingleActionResult(SaveEntry Save, bool Success, string? Error);

public sealed record RetirementResult(
    string PlaythroughKey,
    RetirementMode Mode,
    IReadOnlyList<SingleActionResult> Results)
{
    public int SuccessCount => Results.Count(r => r.Success);
    public int FailureCount => Results.Count(r => !r.Success);
}

/// <summary>
/// Actions on individual saves and whole characters that are NOT part of the
/// regular "keep last N" cleanup: manual single-save store/delete, and
/// retiring an entire character (playthroughID) either by archiving
/// everything to storage or by deleting everything outright.
///
/// Safety: DeleteSave/DeleteFolder only ever delete a path that is actually
/// nested under one of the caller-supplied managed roots (the configured
/// save-dir / storage-dir). This is defense in depth so a future bug
/// elsewhere can never turn into deleting something outside the app's own
/// managed folders.
/// </summary>
public sealed class SaveActionService
{
    private readonly SafeFolderMover _mover;
    private readonly StoragePathResolver _pathResolver;

    public SaveActionService(SafeFolderMover? mover = null, StoragePathResolver? pathResolver = null)
    {
        _mover = mover ?? new SafeFolderMover();
        _pathResolver = pathResolver ?? new StoragePathResolver();
    }

    /// <summary>Moves a single save (normally a Live one) into storage, namespaced by its playthroughID.</summary>
    public MoveResult StoreSave(SaveEntry save, string storageDir)
    {
        var destParent = _pathResolver.ResolveDestinationParent(storageDir, save);
        var destName = _pathResolver.ResolveDestinationFolderName(destParent, save);
        return _mover.MoveFolder(save.FullPath, destParent, destName);
    }

    /// <summary>
    /// Moves a single save (normally a Storage one) back into the live save
    /// dir. The live dir is flat (no playthroughID subfolders, unlike
    /// storage), so the destination parent is simply saveDir itself.
    ///
    /// CP77 does not require any particular folder-name pattern for a live
    /// save - verified by Werner in-game with an arbitrarily-named folder,
    /// which the game picked up and displayed correctly (the folder name
    /// shows up as the save's subtitle in the in-game load menu). So the
    /// original folder name is kept as-is whenever possible; the same
    /// collision-dedup logic used for storing (append the save's own
    /// timestamp, ResolveDestinationFolderName) is reused here for the case
    /// where a live save already occupies that exact name (slot numbers get
    /// reused across characters over time, so this does happen).
    /// </summary>
    public MoveResult RestoreSave(SaveEntry save, string saveDir)
    {
        var destName = _pathResolver.ResolveDestinationFolderName(saveDir, save);
        return _mover.MoveFolder(save.FullPath, saveDir, destName);
    }

    /// <summary>
    /// Permanently deletes a single save folder. Refuses if the save's path
    /// isn't inside one of the managed roots.
    /// </summary>
    public SingleActionResult DeleteSave(SaveEntry save, IReadOnlyCollection<string> managedRoots)
    {
        if (!IsUnderAnyRoot(save.FullPath, managedRoots))
        {
            return new SingleActionResult(save, false, CoreText.Get("ERR_OUTSIDE_ROOTS", save.FullPath));
        }

        try
        {
            Directory.Delete(save.FullPath, recursive: true);
            return new SingleActionResult(save, true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SingleActionResult(save, false, ex.Message);
        }
    }

    /// <summary>
    /// Retires an entire character. StoreAll moves every live save to storage
    /// (ignoring the normal keep-N rule - the point is nothing stays live for
    /// a retired character). DeleteAll wipes both live and stored saves for
    /// that playthroughID permanently.
    /// </summary>
    public RetirementResult Retire(PlaythroughGroup group, RetirementMode mode, string storageDir, IReadOnlyCollection<string> managedRoots)
    {
        var results = new List<SingleActionResult>();

        switch (mode)
        {
            case RetirementMode.StoreAll:
                foreach (var save in group.Live.ToList())
                {
                    var moveResult = StoreSave(save, storageDir);
                    results.Add(new SingleActionResult(save, moveResult.Success, moveResult.Error));
                }
                break;

            case RetirementMode.DeleteAll:
                foreach (var save in group.Saves.ToList())
                {
                    results.Add(DeleteSave(save, managedRoots));
                }

                // Tidy up the now-empty storage/<playthroughID> namespace folder
                // left behind once all of a character's stored saves are gone -
                // otherwise an empty folder lingers forever.
                if (!string.IsNullOrEmpty(group.PlaythroughKey))
                {
                    var ptidFolder = Path.Combine(storageDir, group.PlaythroughKey);
                    if (Directory.Exists(ptidFolder) && !Directory.EnumerateFileSystemEntries(ptidFolder).Any())
                    {
                        try { Directory.Delete(ptidFolder); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* best-effort tidy-up only */ }
                    }
                }
                break;
        }

        return new RetirementResult(group.PlaythroughKey, mode, results);
    }

    /// <summary>
    /// Strictly below one of the roots (never the root itself), a trailing
    /// separator on a root is fine (S3), empty roots are ignored instead of
    /// throwing (S7), and no junction/symlink may sit between root and target (S2/S3).
    /// </summary>
    private static bool IsUnderAnyRoot(string path, IReadOnlyCollection<string> roots) =>
        roots.Any(root => PathSafety.IsSafelyUnder(path, root));
}
