using System.Text.Json;

namespace Cp77SaveManager.Core.Configuration;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configFilePath;

    public ConfigService(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? DefaultPaths.ConfigFilePath;
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt config must never crash the app on startup - fall back to
            // defaults. The user can fix paths again via the UI.
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);

        // Write to a temp file and swap in, so a crash mid-write never leaves
        // a truncated/corrupt config.json behind.
        var tempPath = _configFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _configFilePath, overwrite: true);
    }
}
