using System.IO;
using System.ServiceProcess;
using System.Text.Json;
using SorenB.TenantSwitcher.Models;

namespace SorenB.TenantSwitcher.Services;

/// <summary>
/// Reads which Orchestrator the local Robot is connected to from the Robot's own
/// UiPath.settings (key UiPathServerUrl) and raises <see cref="Changed"/> when it changes.
/// The Studio extension reads the same file, so both tools show the real connection no matter
/// which one switched. The file belongs to UiPath: it is only ever read here.
/// </summary>
public sealed class RobotConnection : IDisposable
{
    private const string FileName = "UiPath.settings";
    private const string ServerUrlKey = "UiPathServerUrl";
    private const string RobotServiceName = "UiPath Robot";

    private readonly IReadOnlyList<string> _paths;
    private readonly Lazy<bool> _hasRobotService;
    private readonly List<FileSystemWatcher> _watchers = new();
    private Timer? _debounce;

    /// <param name="paths">Candidate settings files, service-mode file first. Defaults to <see cref="DefaultPaths"/>.</param>
    /// <param name="hasRobotService">Whether the UiPath Robot Windows service exists (service-mode install).</param>
    public RobotConnection(IReadOnlyList<string>? paths = null, Func<bool>? hasRobotService = null)
    {
        _paths = paths ?? DefaultPaths;
        _hasRobotService = new Lazy<bool>(hasRobotService ?? RobotServiceExists);
    }

    /// <summary>%ProgramData%\UiPath\UiPath.settings (service mode), then %AppData%\UiPath\UiPath.settings (user mode).</summary>
    public static IReadOnlyList<string> DefaultPaths { get; } = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UiPath", FileName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UiPath", FileName),
    };

    /// <summary>Raised (on a worker thread) 300 ms after a settings file changes.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The Robot's Orchestrator URL: a URL when connected, "" when disconnected, or null when no
    /// settings file could be read (unknown).
    /// </summary>
    public string? ReadServerUrl()
    {
        // In service mode the service's file is authoritative; a user-mode file may be left over.
        if (_hasRobotService.Value && ReadServerUrlFrom(_paths[0]) is { } serviceUrl)
        {
            return serviceUrl;
        }

        return _paths
            .Select(path => (Path: path, Url: ReadServerUrlFrom(path)))
            .Where(c => c.Url is not null)
            .OrderByDescending(c => SafeLastWrite(c.Path))
            .Select(c => c.Url)
            .FirstOrDefault();
    }

    /// <summary>UiPathServerUrl from one file: the URL, "" when empty, or null when missing, unreadable or invalid.</summary>
    public static string? ReadServerUrlFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            using var doc = JsonDocument.Parse(reader.ReadToEnd().TrimStart('﻿'));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(ServerUrlKey, out var value)
                   && value.ValueKind is JsonValueKind.String or JsonValueKind.Null
                ? (value.GetString() ?? string.Empty).Trim()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The preset the Robot is connected to. Matches URLs with <see cref="OrchestratorUrl.UrlKey"/>;
    /// among presets with the same URL, the last one switched to wins. When the settings could
    /// not be read (<paramref name="serverUrl"/> null), falls back to the last switched preset.
    /// Same rule as the extension's activePreset().
    /// </summary>
    public static Preset? ResolveActive(IReadOnlyList<Preset> presets, string? serverUrl, Guid? lastActiveId)
    {
        if (serverUrl is null)
        {
            return presets.FirstOrDefault(p => p.Id == lastActiveId);
        }

        if (serverUrl.Length == 0)
        {
            return null;
        }

        var key = OrchestratorUrl.UrlKey(serverUrl);
        var matches = presets.Where(p => OrchestratorUrl.UrlKey(p.OrchestratorUrl) == key).ToList();
        return matches.FirstOrDefault(p => p.Id == lastActiveId) ?? matches.FirstOrDefault();
    }

    /// <summary>Starts raising <see cref="Changed"/> when a settings file changes. Folders that do not exist are skipped.</summary>
    public void StartWatching()
    {
        if (_watchers.Count > 0)
        {
            return;
        }

        _debounce = new Timer(_ => Changed?.Invoke(this, EventArgs.Empty));
        foreach (var directory in _paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (directory is null || !Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                var watcher = new FileSystemWatcher(directory, FileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                };
                FileSystemEventHandler onChange = (_, _) => _debounce.Change(300, Timeout.Infinite);
                watcher.Changed += onChange;
                watcher.Created += onChange;
                watcher.Deleted += onChange;
                watcher.Renamed += (_, _) => _debounce.Change(300, Timeout.Infinite);
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Watching is a convenience; the state is still read on reload and after a switch.
            }
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        _debounce?.Dispose();
    }

    private static DateTime SafeLastWrite(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    private static bool RobotServiceExists()
    {
        try
        {
            return ServiceController.GetServices().Any(s => s.ServiceName.Equals(RobotServiceName, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
