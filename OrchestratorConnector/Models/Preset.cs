namespace OrchestratorConnector.Models;

public sealed class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PresetName { get; set; } = string.Empty;
    public string OrchestratorUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
}
