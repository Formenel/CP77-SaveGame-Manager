using System.Text.Json.Serialization;

namespace Cp77SaveManager.Core.Models;

/// <summary>
/// Fields read from a save's metadata.9.json ("Data.metadata" object).
/// Field names/types below were verified against a real metadata.9.json from
/// Werner's save directory - not guessed. Everything is nullable/optional
/// because older saves, mods or future game patches may omit fields; a
/// missing field must never crash the scanner.
///
/// Notably absent: there is NO player-name field. CP77 does not store a
/// custom character name anywhere in this file - the only stable per-character
/// key is PlaythroughId.
/// </summary>
public sealed class SaveMetadata
{
    [JsonPropertyName("playthroughID")]
    public string? PlaythroughId { get; set; }

    [JsonPropertyName("lifePath")]
    public string? LifePath { get; set; }

    [JsonPropertyName("bodyGender")]
    public string? BodyGender { get; set; }

    [JsonPropertyName("brainGender")]
    public string? BrainGender { get; set; }

    [JsonPropertyName("level")]
    public double? Level { get; set; }

    [JsonPropertyName("streetCred")]
    public double? StreetCred { get; set; }

    [JsonPropertyName("playTime")]
    public double? PlayTimeSeconds { get; set; }

    [JsonPropertyName("timestampString")]
    public string? TimestampString { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("saveVersion")]
    public int? SaveVersion { get; set; }

    [JsonPropertyName("gameVersion")]
    public int? GameVersion { get; set; }

    [JsonPropertyName("buildPatch")]
    public string? BuildPatch { get; set; }

    [JsonPropertyName("difficulty")]
    public string? Difficulty { get; set; }

    [JsonPropertyName("trackedQuest")]
    public string? TrackedQuest { get; set; }

    [JsonPropertyName("locationName")]
    public string? LocationName { get; set; }

    [JsonPropertyName("isCheckpoint")]
    public bool? IsCheckpoint { get; set; }

    [JsonPropertyName("isEndGameSave")]
    public bool? IsEndGameSave { get; set; }

    [JsonPropertyName("isPointOfNoReturn")]
    public bool? IsPointOfNoReturn { get; set; }

    [JsonPropertyName("isModded")]
    public bool? IsModded { get; set; }

    [JsonPropertyName("fileSize")]
    public long? FileSize { get; set; }

    [JsonPropertyName("additionalContentIds")]
    public List<string>? AdditionalContentIds { get; set; }

    /// <summary>
    /// Best-effort parse of timestampString ("11:47:09, 27.09.2026", verified
    /// format: HH:mm:ss, dd.MM.yyyy). The game writes this in the local wall-clock
    /// time of the PC that made the save, so it's interpreted as Local and
    /// converted to UTC to stay comparable with Directory.LastWriteTimeUtc.
    /// Returns null rather than throwing if the format ever changes - callers
    /// must fall back to file mtime.
    /// </summary>
    public DateTime? TryGetTimestampUtc()
    {
        if (string.IsNullOrWhiteSpace(TimestampString)) return null;
        if (!DateTime.TryParseExact(
            TimestampString,
            "HH:mm:ss, dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var result))
        {
            return null;
        }

        return DateTime.SpecifyKind(result, DateTimeKind.Local).ToUniversalTime();
    }
}

public sealed class SaveMetadataContainer
{
    [JsonPropertyName("metadata")]
    public SaveMetadata? Metadata { get; set; }
}

public sealed class SaveMetadataRoot
{
    [JsonPropertyName("RootType")]
    public string? RootType { get; set; }

    [JsonPropertyName("Data")]
    public SaveMetadataContainer? Data { get; set; }
}
