using Cp77SaveManager.Core.Configuration;
using Cp77SaveManager.Core.Localization;
using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.Core.Cleanup;

public sealed record CleanupPlanItem(SaveEntry Save, string Reason);

/// <summary>
/// Result of planning a cleanup for one character (one PlaythroughGroup).
/// ToMove are candidates to move Live -> Storage; nothing is touched here,
/// planning is pure and side-effect free so it can be previewed in a dialog
/// before the user confirms.
/// </summary>
public sealed record CleanupPlan(
    string PlaythroughKey,
    IReadOnlyList<CleanupPlanItem> ToMove,
    IReadOnlyList<SaveEntry> KeptLive);

public sealed class CleanupPlanner
{
    /// <summary>
    /// Plans cleanup for a single character's live saves.
    ///
    /// Rule (Werner's spec): among the save types in rule.SelectedTypes, keep
    /// the rule.KeepTotal most recent saves live IN TOTAL (mixed types count
    /// against the same pool, not counted per type). Save types NOT in
    /// SelectedTypes are never touched, regardless of how many exist.
    /// </summary>
    public CleanupPlan Plan(string playthroughKey, IEnumerable<SaveEntry> liveSavesForCharacter, CleanupRuleConfig rule)
    {
        var selected = rule.SelectedTypes.ToHashSet();

        var eligible = liveSavesForCharacter
            .Where(s => selected.Contains(s.SaveType))
            .OrderByDescending(s => s.EffectiveTimestampUtc)
            .ToList();

        var untouched = liveSavesForCharacter
            .Where(s => !selected.Contains(s.SaveType))
            .ToList();

        var keep = eligible.Take(Math.Max(rule.KeepTotal, 0)).ToList();
        var move = eligible.Skip(Math.Max(rule.KeepTotal, 0)).ToList();

        var typesLabel = string.Join("+", selected.Select(t => t.ToString()));
        var toMove = move
            .Select(s => new CleanupPlanItem(s, CoreText.Get("CLEANUP_REASON", rule.KeepTotal, typesLabel)))
            .ToList();

        var kept = keep.Concat(untouched).ToList();

        return new CleanupPlan(playthroughKey, toMove, kept);
    }
}
