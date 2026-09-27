using System.Text.Json;
using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.Core.Scanning;

public static class MetadataReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false
    };

    /// <summary>
    /// Reads and parses a save's metadata.9.json. Returns null on any failure
    /// (missing file, malformed JSON, unexpected shape) - a corrupt or exotic
    /// save must never crash the scan, it just shows up as "Unknown" in the UI.
    /// </summary>
    public static SaveMetadata? TryRead(string metadataJsonPath)
    {
        try
        {
            if (!File.Exists(metadataJsonPath)) return null;
            using var stream = File.OpenRead(metadataJsonPath);
            var root = JsonSerializer.Deserialize<SaveMetadataRoot>(stream, Options);
            return root?.Data?.Metadata;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
