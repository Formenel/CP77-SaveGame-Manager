namespace Cp77SaveManager.Core.Localization;

/// <summary>
/// Resolves a string key through three fallback stages, per key (not per
/// file - one missing key in an otherwise-loaded language file falls back
/// on its own, the rest of that file's translations still apply):
///   1) the selected language's own file
///   2) de-DE.json (the reference file the completion-% is measured against)
///   3) LocalizationDefaults - hard-coded in this assembly, so the app can
///      never end up with neither a readable langs\ folder nor any text to
///      show, even if that folder is missing or every file in it is broken.
/// </summary>
public sealed class Translator
{
    private readonly IReadOnlyDictionary<string, string> _selected;
    private readonly IReadOnlyDictionary<string, string> _reference;
    private readonly IReadOnlyDictionary<string, string> _defaults;

    public Translator(
        IReadOnlyDictionary<string, string> selected,
        IReadOnlyDictionary<string, string> reference,
        IReadOnlyDictionary<string, string> defaults)
    {
        _selected = selected;
        _reference = reference;
        _defaults = defaults;
    }

    /// <summary>Plain lookup, no formatting.</summary>
    public string Get(string key)
    {
        if (_selected.TryGetValue(key, out var v1) && !string.IsNullOrEmpty(v1)) return v1;
        if (_reference.TryGetValue(key, out var v2) && !string.IsNullOrEmpty(v2)) return v2;
        if (_defaults.TryGetValue(key, out var v3) && !string.IsNullOrEmpty(v3)) return v3;

        // Should never happen as long as LocalizationDefaults stays complete -
        // a visible bracketed key beats a crash or a silently blank label.
        return $"[{key}]";
    }

    /// <summary>Looks up key, then runs the result through string.Format with args.</summary>
    public string Get(string key, params object?[] args) => string.Format(Get(key), args);
}
