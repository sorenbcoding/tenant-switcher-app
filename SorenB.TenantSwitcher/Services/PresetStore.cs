using System.IO;
using System.Text.Json;
using SorenB.TenantSwitcher.Models;

namespace SorenB.TenantSwitcher.Services;

/// <summary>
/// Loads and saves the non-secret preset data in %AppData%\OrchestratorConnector\presets.json
/// (a bare JSON array with PascalCase keys). Client secrets are never written here - see
/// <see cref="CredentialService"/>. The Studio extension reads and writes the same file, so
/// every mutation re-reads it first and <see cref="Changed"/> fires when it changes on disk.
/// </summary>
public sealed class PresetStore : IDisposable
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;

    public PresetStore(string? filePathOverride = null)
    {
        FilePath = filePathOverride ?? GetDefaultFilePath();
    }

    public string FilePath { get; }

    /// <summary>Raised (on a worker thread) 200 ms after presets.json changes on disk.</summary>
    public event EventHandler? Changed;

    public static string GetDefaultFilePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            // Shared with the Tenant Switcher for UiPath Studio extension. Do not rename.
            "OrchestratorConnector");
        return Path.Combine(directory, "presets.json");
    }

    /// <exception cref="InvalidDataException">presets.json is not a JSON array of presets.</exception>
    public async Task<List<Preset>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
        {
            return new List<Preset>();
        }

        // Read with sharing so a concurrent atomic write from the extension does not fail us.
        string text;
        using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
        {
            text = (await reader.ReadToEndAsync(ct).ConfigureAwait(false)).TrimStart('﻿').Trim();
        }

        if (text.Length == 0)
        {
            return new List<Preset>();
        }

        try
        {
            var presets = JsonSerializer.Deserialize<List<Preset>>(text, ReadOptions) ?? new List<Preset>();
            return presets.Where(p => p.Id != Guid.Empty).ToList();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{FilePath} is not valid JSON.", ex);
        }
    }

    /// <summary>
    /// Inserts or updates a preset, matched by id and otherwise by name (like the extension).
    /// Returns the stored preset.
    /// </summary>
    public async Task<Preset> UpsertAsync(Preset input, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var presets = await LoadAsync(ct).ConfigureAwait(false);
            var name = input.PresetName.Trim();
            var index = presets.FindIndex(p => p.Id == input.Id);
            if (index < 0)
            {
                index = presets.FindIndex(p => PresetValidation.SameName(p.PresetName, name));
            }

            var stored = new Preset
            {
                Id = index >= 0 ? presets[index].Id : input.Id,
                PresetName = name,
                OrchestratorUrl = input.OrchestratorUrl.Trim(),
                ClientId = input.ClientId.Trim(),
            };

            if (index >= 0)
            {
                presets[index] = stored;
            }
            else
            {
                presets.Add(stored);
            }

            await SaveAsync(presets, ct).ConfigureAwait(false);
            return stored;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var presets = await LoadAsync(ct).ConfigureAwait(false);
            await SaveAsync(presets.Where(p => p.Id != id), ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Writes the whole list atomically (temp file, then replace).</summary>
    public async Task SaveAsync(IEnumerable<Preset> presets, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        var tempFilePath = FilePath + ".tmp";
        using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, presets.ToList(), WriteOptions, ct).ConfigureAwait(false);
        }

        File.Move(tempFilePath, FilePath, overwrite: true);
    }

    /// <summary>Starts raising <see cref="Changed"/> when presets.json changes on disk.</summary>
    public void StartWatching()
    {
        if (_watcher is not null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            _debounce = new Timer(_ => Changed?.Invoke(this, EventArgs.Empty));
            _watcher = new FileSystemWatcher(directory, Path.GetFileName(FilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            FileSystemEventHandler onChange = (_, _) => _debounce.Change(200, Timeout.Infinite);
            _watcher.Changed += onChange;
            _watcher.Created += onChange;
            _watcher.Deleted += onChange;
            _watcher.Renamed += (_, _) => _debounce.Change(200, Timeout.Infinite);
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Watching is a convenience; the list still reloads after the app's own changes.
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        _gate.Dispose();
    }
}
