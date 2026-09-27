using System.Text.Json.Serialization;

namespace Cp77SaveManager.Core.Localization;

/// <summary>
/// On-disk shape of one langs\*.json file. Deliberately flat and
/// hand-editable: two top-level fields, no nesting, plain string->string map.
/// A translator with no dev tooling can copy an existing file, change LANG
/// and the values, save as a new file name, done.
/// </summary>
public sealed class LanguageFile
{
    /// <summary>Display name shown in the language menu, e.g. "Deutsch" or "English".</summary>
    [JsonPropertyName("LANG")]
    public string Lang { get; set; } = "";

    [JsonPropertyName("STRINGS")]
    public Dictionary<string, string> Strings { get; set; } = new();
}
