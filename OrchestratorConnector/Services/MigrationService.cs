using System.IO;
using System.Text.Json;
using OrchestratorConnector.Models;

namespace OrchestratorConnector.Services;

/// <summary>
/// One-time migration from the pre-refactor plaintext presets.json (written next to the exe)
/// into the new %AppData% store plus Credential Manager. Never throws - a failed migration
/// must not prevent the app from starting, and leaves the legacy file untouched so it can be
/// retried or the presets re-entered by hand.
/// </summary>
public static class MigrationService
{
    private const string LegacyFileName = "presets.json";

    public static void TryMigrateLegacyPresets(string legacySearchDirectory, PresetStore newStore)
    {
        try
        {
            if (File.Exists(PresetStore.GetDefaultFilePath()))
            {
                return;
            }

            var legacyFilePath = Path.Combine(legacySearchDirectory, LegacyFileName);
            if (!File.Exists(legacyFilePath))
            {
                return;
            }

            var json = File.ReadAllText(legacyFilePath);
            var legacyPresets = JsonSerializer.Deserialize<List<LegacyPreset>>(json);
            if (legacyPresets is null || legacyPresets.Count == 0)
            {
                return;
            }

            var migrated = new List<Preset>();
            foreach (var legacy in legacyPresets)
            {
                var preset = new Preset
                {
                    Id = Guid.NewGuid(),
                    PresetName = legacy.PresetName,
                    OrchestratorUrl = legacy.Option1,
                    ClientId = legacy.Option2,
                };

                if (!string.IsNullOrEmpty(legacy.Option3))
                {
                    CredentialService.SaveSecret(preset.Id, legacy.Option3);
                }

                migrated.Add(preset);
            }

            newStore.SaveAsync(migrated).GetAwaiter().GetResult();

            var verification = newStore.LoadAsync().GetAwaiter().GetResult();
            if (verification.Count == migrated.Count)
            {
                File.Delete(legacyFilePath);
            }
        }
        catch
        {
            // Migration must never block startup; the legacy file is left in place on failure.
        }
    }
}
