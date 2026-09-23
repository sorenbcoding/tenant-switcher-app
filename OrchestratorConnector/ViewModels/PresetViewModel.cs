using OrchestratorConnector.Models;

namespace OrchestratorConnector.ViewModels;

public sealed class PresetViewModel : ViewModelBase
{
    private string _presetName;
    private string _orchestratorUrl;
    private string _clientId;

    public PresetViewModel(Preset preset)
    {
        Id = preset.Id;
        _presetName = preset.PresetName;
        _orchestratorUrl = preset.OrchestratorUrl;
        _clientId = preset.ClientId;
    }

    public Guid Id { get; }

    public string PresetName
    {
        get => _presetName;
        set => SetField(ref _presetName, value);
    }

    public string OrchestratorUrl
    {
        get => _orchestratorUrl;
        set => SetField(ref _orchestratorUrl, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetField(ref _clientId, value);
    }

    public Preset ToModel() => new()
    {
        Id = Id,
        PresetName = PresetName,
        OrchestratorUrl = OrchestratorUrl,
        ClientId = ClientId,
    };
}
