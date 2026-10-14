using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SorenB.TenantSwitcher.Models;

namespace SorenB.TenantSwitcher.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>User settings. Mirrors the Studio extension's tenantSwitcher.* settings.</summary>
public sealed class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public bool ConfirmBeforeSwitch { get; set; } = true;
    public bool ValidateBeforeSwitch { get; set; } = true;
    public bool RestartAssistant { get; set; } = true;
    public string UiRobotPath { get; set; } = string.Empty;
    public string UipCliPath { get; set; } = "uip";
}

/// <summary>App-only options for one preset (the extension keeps its own copy in its storage).</summary>
public sealed class PresetOptions
{
    public bool SyncUipCli { get; set; }
}

/// <summary>State that survives restarts: the active preset, the undismissed last result and per-preset options.</summary>
public sealed class AppState
{
    public Guid? ActivePresetId { get; set; }
    public SwitchResult? LastResult { get; set; }
    public Dictionary<Guid, PresetOptions> Presets { get; set; } = new();
}

/// <summary>
/// Reads and writes settings.json and state.json in %LocalAppData%\SorenB\TenantSwitcher.
/// Reads never throw: a missing or corrupt file gives the defaults.
/// </summary>
public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppStateStore(string? directoryOverride = null)
    {
        Directory = directoryOverride ?? DefaultDirectory;
    }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SorenB", "TenantSwitcher");

    public string Directory { get; }

    private string SettingsPath => Path.Combine(Directory, "settings.json");

    private string StatePath => Path.Combine(Directory, "state.json");

    public AppSettings LoadSettings() => Read<AppSettings>(SettingsPath);

    public void SaveSettings(AppSettings settings) => Write(SettingsPath, settings);

    public AppState LoadState() => Read<AppState>(StatePath);

    public void SaveState(AppState state) => Write(StatePath, state);

    private static T Read<T>(string path) where T : new()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T()
                : new T();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new T();
        }
    }

    private void Write<T>(string path, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
