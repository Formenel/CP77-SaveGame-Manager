using System.Text.Encodings.Web;
using System.Text.Json;

namespace Cp77SaveManager.Core.Localization;

/// <summary>
/// Scans a langs\ folder of hand-editable JSON files, resolves the active
/// language's fallback chain, and (Werner's explicit call) writes a fresh
/// de-DE.json from LocalizationDefaults if that reference file is missing,
/// rather than treating a missing reference as an error.
/// </summary>
public sealed class LocalizationService
{
    public const string ReferenceCode = "de-DE";

    // System.Text.Json's default encoder escapes every non-ASCII character
    // (ö -> ö, ß -> ß, ...) as a defensive default for text that
    // might end up embedded in HTML/JS. This file is never embedded
    // anywhere, so that's pure noise for a human hand-editing it -
    // UnsafeRelaxedJsonEscaping only relaxes that same-purpose escaping
    // (still escapes <, >, &, ' the way plain ASCII JSON always would);
    // it doesn't change what's valid JSON or what File.ReadAllText/
    // JsonSerializer read back - literal UTF-8 and \uXXXX decode identically.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _langsDir;

    public LocalizationService(string langsDir)
    {
        _langsDir = langsDir;
    }

    /// <summary>
    /// Writes langs\de-DE.json from LocalizationDefaults if it doesn't
    /// already exist. Call once at startup, before ScanAvailableLanguages/
    /// CreateTranslator, so de-DE is always present as the completion-%
    /// reference and as a selectable language even on a brand-new install
    /// with no langs\ folder at all.
    ///
    /// An existing de-DE.json only gets keys added that it's missing (new
    /// strings from an app update) - existing values are never touched.
    ///
    /// Best-effort (S6): if the exe folder isn't writable (e.g. Program Files)
    /// this silently does nothing - the built-in defaults still cover every
    /// key and ScanAvailableLanguages still lists "Deutsch".
    /// </summary>
    public void EnsureReferenceFileExists()
    {
        try
        {
            var path = ReferenceFilePath;
            var existing = TryLoad(path);
            LanguageFile file;
            if (existing is null)
            {
                if (File.Exists(path)) return; // exists but unreadable/broken - don't overwrite a human's file
                file = new LanguageFile { Lang = "Deutsch", Strings = new Dictionary<string, string>(LocalizationDefaults.Strings) };
            }
            else
            {
                var missing = LocalizationDefaults.Strings.Where(kv => !existing.Strings.ContainsKey(kv.Key)).ToList();
                if (missing.Count == 0) return;
                foreach (var (key, value) in missing) existing.Strings[key] = value;
                file = existing;
            }

            Directory.CreateDirectory(_langsDir);
            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // read-only install location - defaults are used, see summary
        }
    }

    private string ReferenceFilePath => Path.Combine(_langsDir, ReferenceCode + ".json");

    /// <summary>
    /// Every *.json in langs\ that parses and has a non-empty LANG field,
    /// with its completion-% against the de-DE reference. A file that
    /// doesn't parse, or has no LANG (e.g. an empty "{}" placeholder), is
    /// silently skipped - never shown as a nameless/broken menu entry.
    /// </summary>
    public IReadOnlyList<LanguageInfo> ScanAvailableLanguages()
    {
        var result = new List<LanguageInfo>();
        var referenceKeys = LoadReferenceKeys();

        if (Directory.Exists(_langsDir))
        {
            foreach (var path in Directory.EnumerateFiles(_langsDir, "*.json"))
            {
                var file = TryLoad(path);
                if (file is null || string.IsNullOrWhiteSpace(file.Lang)) continue;

                var code = Path.GetFileNameWithoutExtension(path);
                var percent = ComputeCompletion(file.Strings, referenceKeys);
                result.Add(new LanguageInfo(code, file.Lang, percent, path));
            }
        }

        // German is always selectable, even if de-DE.json couldn't be written
        // (S6) - otherwise a user who switched away could never switch back.
        if (!result.Any(l => l.Code.Equals(ReferenceCode, StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new LanguageInfo(ReferenceCode, "Deutsch", 100, string.Empty));
        }

        return result.OrderBy(l => l.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Builds the 3-stage fallback Translator for one language code (a
    /// LanguageInfo.Code / AppConfig.Language value). Missing/unreadable
    /// files at any stage just mean an empty dictionary for that stage -
    /// Translator.Get still falls through to LocalizationDefaults.
    /// </summary>
    public Translator CreateTranslator(string code)
    {
        var selected = TryLoad(Path.Combine(_langsDir, code + ".json"))?.Strings
                       ?? new Dictionary<string, string>();
        var reference = code == ReferenceCode
            ? selected
            : TryLoad(ReferenceFilePath)?.Strings ?? new Dictionary<string, string>();

        return new Translator(selected, reference, LocalizationDefaults.Strings);
    }

    private IReadOnlyDictionary<string, string> LoadReferenceKeys()
    {
        var reference = TryLoad(ReferenceFilePath);
        return reference is not null && reference.Strings.Count > 0 ? reference.Strings : LocalizationDefaults.Strings;
    }

    private static int ComputeCompletion(Dictionary<string, string> candidate, IReadOnlyDictionary<string, string> reference)
    {
        if (reference.Count == 0) return 100;
        var translated = reference.Keys.Count(k => candidate.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v));
        return (int)Math.Round(translated * 100.0 / reference.Count, MidpointRounding.AwayFromZero);
    }

    private static LanguageFile? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<LanguageFile>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null; // broken JSON - treated as "not available", scan/translator never crash over it
        }
    }
}
