using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SorenB.TenantSwitcher.Services;

public enum StartMenuChange
{
    /// <summary>The setting was on and is now off.</summary>
    Changed,

    /// <summary>The setting was already off, or not in the file.</summary>
    Unchanged,

    /// <summary>There is no settings file.</summary>
    Missing,
}

/// <summary>
/// UiPath Assistant's own preferences (%AppData%\UiPath\agent-settings.json). With
/// agent.showStartMenuShortcuts on, Assistant rewrites one Start menu shortcut per automation
/// whenever it connects to another tenant, which can flood Windows Explorer and freeze it.
/// The file belongs to Assistant: <see cref="DisableStartMenuShortcuts"/> is only called while
/// Assistant is closed and the user has asked for it. Mirrors the Studio extension's
/// scripts/assistantSettings.ps1.
/// </summary>
public static class AssistantSettings
{
    public const string WarningText =
        "UiPath Assistant shows your automations in the Windows Start menu. When it connects to another tenant it rewrites all those shortcuts, which can freeze Windows Explorer until it is restarted.";

    private static readonly Regex ShowStartMenuShortcutsOn = new("(\"showStartMenuShortcuts\"\\s*:\\s*)true\\b", RegexOptions.CultureInvariant);

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UiPath", "agent-settings.json");

    /// <summary>agent.showStartMenuShortcuts, or null when the file or key is missing or invalid.</summary>
    public static bool? ReadShowStartMenuShortcuts(string? path = null)
    {
        try
        {
            // ReadAllText drops a byte order mark, which JsonDocument would reject.
            using var doc = JsonDocument.Parse(File.ReadAllText(path ?? DefaultPath));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("agent", out var agent)
                && agent.ValueKind == JsonValueKind.Object
                && agent.TryGetProperty("showStartMenuShortcuts", out var value)
                && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? value.GetBoolean()
                    : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sets agent.showStartMenuShortcuts to false. Only that value changes; every other byte,
    /// including a byte order mark, stays as Assistant wrote it.
    /// </summary>
    public static StartMenuChange DisableStartMenuShortcuts(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
        {
            return StartMenuChange.Missing;
        }

        var bytes = File.ReadAllBytes(path);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        if (!ShowStartMenuShortcutsOn.IsMatch(text))
        {
            return StartMenuChange.Unchanged;
        }

        var updated = ShowStartMenuShortcutsOn.Replace(text, "${1}false");
        using (JsonDocument.Parse(updated))
        {
            // Never leave Assistant with a file it cannot parse.
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, updated, new UTF8Encoding(bom));
        File.Move(tmp, path, overwrite: true);
        return StartMenuChange.Changed;
    }
}
