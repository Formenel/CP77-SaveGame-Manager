using Cp77SaveManager.Core.Storage;

namespace Cp77SaveManager.Core.Cleanup;

public sealed record CleanupExecutionResult(CleanupPlanItem Item, MoveResult MoveResult);

/// <summary>
/// Executes an already-approved CleanupPlan: moves each ToMove item from the
/// live save dir into storage via SafeFolderMover (copy/verify/delete),
/// namespaced by playthroughID via StoragePathResolver. One failure does not
/// abort the rest of the batch - each save is independent, so the UI can show
/// "12 of 13 moved, 1 failed: <reason>" rather than losing partial progress.
/// </summary>
public sealed class CleanupExecutor
{
    private readonly SafeFolderMover _mover;
    private readonly StoragePathResolver _pathResolver;

    public CleanupExecutor(SafeFolderMover? mover = null, StoragePathResolver? pathResolver = null)
    {
        _mover = mover ?? new SafeFolderMover();
        _pathResolver = pathResolver ?? new StoragePathResolver();
    }

    public IReadOnlyList<CleanupExecutionResult> Execute(CleanupPlan plan, string storageDir)
    {
        var results = new List<CleanupExecutionResult>();

        foreach (var item in plan.ToMove)
        {
            var destParent = _pathResolver.ResolveDestinationParent(storageDir, item.Save);
            var destName = _pathResolver.ResolveDestinationFolderName(destParent, item.Save);

            var moveResult = _mover.MoveFolder(item.Save.FullPath, destParent, destName);
            results.Add(new CleanupExecutionResult(item, moveResult));
        }

        return results;
    }
}
