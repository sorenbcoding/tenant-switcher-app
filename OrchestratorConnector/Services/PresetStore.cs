using System.IO;
using System.Text.Json;
using OrchestratorConnector.Models;

namespace OrchestratorConnector.Services;

/// <summary>
/// Loads and saves the non-secret preset metadata to %AppData%\OrchestratorConnector\presets.json.
/// Client secrets are never written here - see <see cref="CredentialService"/>.
/// </summary>
public sealed class PresetStore
{
    private readonly string _filePath;

    public PresetStore(string? filePathOverride = null)
    {
        _filePath = filePathOverride ?? GetDefaultFilePath();
    }

    public static string GetDefaultFilePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OrchestratorConnector");
        return Path.Combine(directory, "presets.json");
    }

    public async Task<List<Preset>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            return new List<Preset>();
        }

        await using var stream = File.OpenRead(_filePath);
        var presets = await JsonSerializer.DeserializeAsync<List<Preset>>(stream, cancellationToken: ct);
        return presets ?? new List<Preset>();
    }

    public async Task SaveAsync(IEnumerable<Preset> presets, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        var tempFilePath = _filePath + ".tmp";
        var options = new JsonSerializerOptions { WriteIndented = true };

        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, presets.ToList(), options, ct);
        }

        File.Move(tempFilePath, _filePath, overwrite: true);
    }
}
