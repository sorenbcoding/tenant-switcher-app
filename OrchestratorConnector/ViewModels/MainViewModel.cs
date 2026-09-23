using System.Collections.ObjectModel;
using System.Windows.Input;
using OrchestratorConnector.Models;
using OrchestratorConnector.Services;

namespace OrchestratorConnector.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly PresetStore _store;
    private readonly OrchestratorSwitcher _switcher;

    private PresetViewModel? _selectedPreset;
    private string _editPresetName = string.Empty;
    private string _editOrchestratorUrl = string.Empty;
    private string _editClientId = string.Empty;
    private string _editClientSecret = string.Empty;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public MainViewModel(PresetStore store, OrchestratorSwitcher switcher)
    {
        _store = store;
        _switcher = switcher;
        _switcher.ProgressChanged += (_, progress) => StatusMessage = progress.Message;

        SaveCommand = new RelayCommand(_ => SavePreset());
        EditCommand = new RelayCommand(_ => LoadSelectedIntoEditFields(), _ => SelectedPreset is not null);
        DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedPreset is not null);
        ConnectCommand = new AsyncRelayCommand(_ => ConnectAsync(), _ => SelectedPreset is not null && !IsBusy);
    }

    public ObservableCollection<PresetViewModel> Presets { get; } = new();

    public PresetViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set => SetField(ref _selectedPreset, value);
    }

    public string EditPresetName
    {
        get => _editPresetName;
        set => SetField(ref _editPresetName, value);
    }

    public string EditOrchestratorUrl
    {
        get => _editOrchestratorUrl;
        set => SetField(ref _editOrchestratorUrl, value);
    }

    public string EditClientId
    {
        get => _editClientId;
        set => SetField(ref _editClientId, value);
    }

    public string EditClientSecret
    {
        get => _editClientSecret;
        set => SetField(ref _editClientSecret, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConnectCommand { get; }

    public async Task LoadAsync()
    {
        var presets = await _store.LoadAsync();
        Presets.Clear();
        foreach (var preset in presets)
        {
            Presets.Add(new PresetViewModel(preset));
        }

        StatusMessage = "Ready.";
    }

    private void SavePreset()
    {
        var name = EditPresetName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusMessage = "Enter a preset name before saving.";
            return;
        }

        var existing = Presets.FirstOrDefault(p => string.Equals(p.PresetName, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.PresetName = name;
            existing.OrchestratorUrl = EditOrchestratorUrl.Trim();
            existing.ClientId = EditClientId.Trim();

            if (!string.IsNullOrEmpty(EditClientSecret))
            {
                CredentialService.SaveSecret(existing.Id, EditClientSecret);
            }

            StatusMessage = "Entry saved!";
        }
        else
        {
            var preset = new PresetViewModel(new Preset
            {
                PresetName = name,
                OrchestratorUrl = EditOrchestratorUrl.Trim(),
                ClientId = EditClientId.Trim(),
            });

            if (!string.IsNullOrEmpty(EditClientSecret))
            {
                CredentialService.SaveSecret(preset.Id, EditClientSecret);
            }

            Presets.Add(preset);
            StatusMessage = "Entry successfully added!";
        }

        ClearEditFields();
        _ = PersistAsync();
    }

    private void LoadSelectedIntoEditFields()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        EditPresetName = SelectedPreset.PresetName;
        EditOrchestratorUrl = SelectedPreset.OrchestratorUrl;
        EditClientId = SelectedPreset.ClientId;
        // Deliberately not pre-filled: editing a preset never re-displays its stored secret.
        EditClientSecret = string.Empty;
    }

    private void DeleteSelected()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        CredentialService.DeleteSecret(SelectedPreset.Id);
        Presets.Remove(SelectedPreset);
        SelectedPreset = null;
        ClearEditFields();
        StatusMessage = "Entry deleted.";
        _ = PersistAsync();
    }

    private async Task ConnectAsync()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        var secret = CredentialService.TryReadSecret(SelectedPreset.Id);
        if (secret is null)
        {
            StatusMessage = "No stored secret for this preset - edit it and re-enter the Client Secret.";
            return;
        }

        IsBusy = true;
        try
        {
            await _switcher.SwitchAsync(SelectedPreset.ToModel(), secret);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearEditFields()
    {
        EditPresetName = string.Empty;
        EditOrchestratorUrl = string.Empty;
        EditClientId = string.Empty;
        EditClientSecret = string.Empty;
    }

    private Task PersistAsync() => _store.SaveAsync(Presets.Select(p => p.ToModel()).ToList());
}
