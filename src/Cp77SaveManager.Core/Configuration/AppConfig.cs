using System.Text.Json.Serialization;
using Cp77SaveManager.Core.Models;

namespace Cp77SaveManager.Core.Configuration;

public sealed class CleanupRuleConfig
{
    /// <summary>Which save types count toward the "keep last N" rule. Default: Auto + Manual.</summary>
    [JsonPropertyName("selectedTypes")]
    public List<SaveType> SelectedTypes { get; set; } = new(SaveTypeExtensions.DefaultCleanupSelection);

    /// <summary>
    /// Total number of saves to keep live per character, counted across all
    /// SelectedTypes combined (not per type) - Werner's explicit call: a mix
    /// of e.g. Auto+Manual saves counts as one pool, newest N stay live.
    /// </summary>
    [JsonPropertyName("keepTotal")]
    public int KeepTotal { get; set; } = 15;
}

/// <summary>
/// Window bounds use plain screen coordinates (Windows' virtual desktop is one
/// continuous coordinate space across all monitors, including negative X/Y for
/// screens placed left of / above the primary one), so a saved X/Y/Width/Height
/// alone is already multi-monitor correct for the normal (non-maximized) case.
/// For maximized windows, restoring Location+Size first and then setting
/// WindowState=Maximized (WinForms convention, using RestoreBounds on save)
/// makes it maximize on whichever monitor that location falls in - no
/// separate "monitor index" needs to be stored.
/// </summary>
public sealed class WindowConfig
{
    [JsonPropertyName("x")]
    public int X { get; set; } = 100;

    [JsonPropertyName("y")]
    public int Y { get; set; } = 100;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1100;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 700;

    [JsonPropertyName("maximized")]
    public bool Maximized { get; set; }
}

public sealed class SaveListConfig
{
    /// <summary>Column widths in pixels, in the ListView's fixed column order.</summary>
    [JsonPropertyName("columnWidths")]
    public List<int>? ColumnWidths { get; set; }

    [JsonPropertyName("sortColumnIndex")]
    public int SortColumnIndex { get; set; } = -1; // -1 = default (date desc)

    [JsonPropertyName("sortAscending")]
    public bool SortAscending { get; set; }
}

public sealed class AppConfig
{
    [JsonPropertyName("saveDir")]
    public string SaveDir { get; set; } = DefaultPaths.DefaultSaveDir;

    [JsonPropertyName("storageDir")]
    public string StorageDir { get; set; } = DefaultPaths.DefaultStorageDir;

    /// <summary>playthroughID -> user-assigned nickname.</summary>
    [JsonPropertyName("nicknames")]
    public Dictionary<string, string> Nicknames { get; set; } = new();

    [JsonPropertyName("cleanupRule")]
    public CleanupRuleConfig CleanupRule { get; set; } = new();

    [JsonPropertyName("window")]
    public WindowConfig Window { get; set; } = new();

    [JsonPropertyName("saveList")]
    public SaveListConfig SaveList { get; set; } = new();
}

public static class DefaultPaths
{
    /// <summary>
    /// %USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077 - confirmed as
    /// Werner's actual save path. "Saved Games" has no cross-platform
    /// SpecialFolder constant in .NET, so it's built explicitly from the user
    /// profile folder rather than guessed via a generic "Documents" path.
    /// </summary>
    public static string DefaultSaveDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Saved Games", "CD Projekt Red", "Cyberpunk 2077");

    /// <summary>
    /// Sibling of the save dir, confirmed as Werner's chosen convention
    /// (he created "CP77SGM-storage" next to "Cyberpunk 2077").
    /// </summary>
    public static string DefaultStorageDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Saved Games", "CD Projekt Red", "CP77SGM-storage");

    /// <summary>
    /// Fixed base config directory (the one thing Werner said should NOT be
    /// configurable): %APPDATA%\cp77sgm. Config here, never next to the exe,
    /// so the exe can live anywhere - including a UAC-protected folder -
    /// without ever needing admin rights.
    /// </summary>
    public static string ConfigBaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "cp77sgm");

    public static string ConfigFilePath => Path.Combine(ConfigBaseDir, "config.json");
}
