namespace SorenB.TenantSwitcher.Models;

/// <summary>
/// Mirrors the pre-refactor on-disk shape of presets.json (plaintext secret, Option1/2/3 fields).
/// Used only by <see cref="SorenB.TenantSwitcher.Services.MigrationService"/>.
/// </summary>
internal sealed class LegacyPreset
{
    public string Option1 { get; set; } = string.Empty;
    public string Option2 { get; set; } = string.Empty;
    public string Option3 { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
}
