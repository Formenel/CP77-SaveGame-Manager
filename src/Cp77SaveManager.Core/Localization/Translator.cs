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
    public string Get(string key) => Candidates(key).FirstOrDefault() ?? $"[{key}]";

    /// <summary>
    /// Looks up key and formats it with args. A broken placeholder in a
    /// hand-edited language file (e.g. "{5}" or a lone "{") must not crash the
    /// app (S7) - it falls through to the next stage instead.
    /// </summary>
    public string Get(string key, params object?[] args)
    {
        foreach (var candidate in Candidates(key))
        {
            try
            {
                return string.Format(candidate, args);
            }
            catch (FormatException)
            {
                // try next stage
            }
        }
        return $"[{key}]";
    }

    private IEnumerable<string> Candidates(string key)
    {
        if (_selected.TryGetValue(key, out var v1) && !string.IsNullOrEmpty(v1)) yield return v1;
        if (_reference.TryGetValue(key, out var v2) && !string.IsNullOrEmpty(v2)) yield return v2;
        if (_defaults.TryGetValue(key, out var v3) && !string.IsNullOrEmpty(v3)) yield return v3;
    }
}
