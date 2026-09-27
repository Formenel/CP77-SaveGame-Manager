namespace Cp77SaveManager.Core.Models;

/// <summary>
/// All saves (Live + Storage) belonging to one playthroughID, i.e. one
/// "character" node in the left-hand tree. The Unknown bucket
/// (PlaythroughKey == "") collects saves whose metadata.9.json is missing or
/// unparsable - never silently dropped.
/// </summary>
public sealed class PlaythroughGroup
{
    public required string PlaythroughKey { get; init; }

    public required IReadOnlyList<SaveEntry> Saves { get; init; }

    /// <summary>User-assigned nickname from config, if any.</summary>
    public string? Nickname { get; init; }

    public IEnumerable<SaveEntry> Live => Saves.Where(s => s.Location == SaveLocation.Live);
    public IEnumerable<SaveEntry> Stored => Saves.Where(s => s.Location == SaveLocation.Storage);

    public bool IsUnknown => string.IsNullOrEmpty(PlaythroughKey);

    /// <summary>
    /// Auto-generated label from the most recent save's metadata, e.g.
    /// "Streetkid / Female / Lvl 12". Falls back to a short PTID excerpt if no
    /// metadata is available at all.
    /// </summary>
    public string AutoLabel
    {
        get
        {
            var newest = Saves.OrderByDescending(s => s.EffectiveTimestampUtc).FirstOrDefault();
            var meta = newest?.Metadata;
            if (meta is null)
            {
                return IsUnknown ? "Unbekannt" : $"PTID {PlaythroughKey[..Math.Min(8, PlaythroughKey.Length)]}";
            }

            var lifePath = meta.LifePath ?? "?";
            var gender = meta.BodyGender ?? "?";
            var level = meta.Level.HasValue ? $"Lvl {meta.Level.Value:0}" : "Lvl ?";
            return $"{lifePath} / {gender} / {level}";
        }
    }

    /// <summary>What the tree/list should actually display: nickname if set, else the auto label.</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(Nickname) ? AutoLabel : Nickname!;

    public static IReadOnlyList<PlaythroughGroup> GroupSaves(
        IEnumerable<SaveEntry> saves,
        IReadOnlyDictionary<string, string>? nicknames = null)
    {
        return saves
            .GroupBy(s => s.PlaythroughKey)
            .Select(g => new PlaythroughGroup
            {
                PlaythroughKey = g.Key,
                Saves = g.ToList(),
                Nickname = nicknames is not null && nicknames.TryGetValue(g.Key, out var nick) ? nick : null
            })
            .OrderBy(g => g.IsUnknown) // unknown bucket last
            .ThenByDescending(g => g.Saves.Max(s => s.EffectiveTimestampUtc))
            .ToList();
    }
}
