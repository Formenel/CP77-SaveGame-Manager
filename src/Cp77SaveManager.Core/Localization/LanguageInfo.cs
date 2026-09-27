namespace Cp77SaveManager.Core.Localization;

/// <summary>
/// One entry for the "Sprache" menu: Code is the file name without extension
/// (e.g. "de-DE") and doubles as the value stored in AppConfig.Language.
/// CompletionPercent is how much of the reference (de-DE) key set this file
/// actually covers with a non-empty value - shown next to the display name,
/// e.g. "English (en-GB, 100%)".
/// </summary>
public sealed record LanguageInfo(string Code, string DisplayName, int CompletionPercent, string FilePath);
