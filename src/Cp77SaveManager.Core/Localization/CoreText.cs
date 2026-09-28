namespace Cp77SaveManager.Core.Localization;

/// <summary>
/// Translator for texts produced inside Core (error messages, cleanup reason,
/// auto labels). The app sets Current on startup and on language switch;
/// without that (tests) the built-in German defaults are used.
/// </summary>
public static class CoreText
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    public static Translator Current { get; set; } = new(Empty, Empty, LocalizationDefaults.Strings);

    public static string Get(string key, params object?[] args) => Current.Get(key, args);
}
