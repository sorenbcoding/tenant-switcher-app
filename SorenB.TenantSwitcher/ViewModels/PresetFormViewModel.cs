using System.Runtime.CompilerServices;
using System.Windows.Input;
using SorenB.TenantSwitcher.Models;
using SorenB.TenantSwitcher.Services;

namespace SorenB.TenantSwitcher.ViewModels;

/// <summary>The inline add/edit form. Typing in a field clears that field's error.</summary>
public sealed class PresetFormViewModel : ViewModelBase
{
    private string _name;
    private string _url;
    private string _clientId;
    private string _secret = string.Empty;
    private bool _syncUipCli;
    private string _nameError = string.Empty;
    private string _urlError = string.Empty;
    private string _clientIdError = string.Empty;
    private string _secretError = string.Empty;
    private string _testResult = string.Empty;
    private bool _testOk;
    private bool _isBusy;

    public PresetFormViewModel(MainViewModel owner, Preset? editing, bool hasStoredSecret, bool syncUipCli)
    {
        EditingId = editing?.Id;
        HasStoredSecret = hasStoredSecret;
        _name = editing?.PresetName ?? string.Empty;
        _url = editing?.OrchestratorUrl ?? string.Empty;
        _clientId = editing?.ClientId ?? string.Empty;
        _syncUipCli = syncUipCli;

        SaveCommand = new AsyncRelayCommand(_ => owner.SaveFormAsync(this), _ => !IsBusy);
        TestCommand = new AsyncRelayCommand(_ => owner.TestFormAsync(this), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => owner.CloseForm(), _ => !IsBusy);
    }

    public Guid? EditingId { get; }

    public bool HasStoredSecret { get; }

    public string Title => EditingId is null ? "Add preset" : "Edit preset";

    public string SecretPlaceholder => HasStoredSecret ? "Stored. Leave blank to keep it." : string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                NameError = string.Empty;
            }
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (SetField(ref _url, value))
            {
                UrlError = string.Empty;
            }
        }
    }

    public string ClientId
    {
        get => _clientId;
        set
        {
            if (SetField(ref _clientId, value))
            {
                ClientIdError = string.Empty;
            }
        }
    }

    public string Secret
    {
        get => _secret;
        set
        {
            if (SetField(ref _secret, value ?? string.Empty))
            {
                SecretError = string.Empty;
            }
        }
    }

    public bool SyncUipCli
    {
        get => _syncUipCli;
        set => SetField(ref _syncUipCli, value);
    }

    public string NameError
    {
        get => _nameError;
        private set => SetError(ref _nameError, value, nameof(HasNameError));
    }

    public string UrlError
    {
        get => _urlError;
        private set => SetError(ref _urlError, value, nameof(HasUrlError));
    }

    public string ClientIdError
    {
        get => _clientIdError;
        private set => SetError(ref _clientIdError, value, nameof(HasClientIdError));
    }

    public string SecretError
    {
        get => _secretError;
        private set => SetError(ref _secretError, value, nameof(HasSecretError));
    }

    public bool HasNameError => _nameError.Length > 0;

    public bool HasUrlError => _urlError.Length > 0;

    public bool HasClientIdError => _clientIdError.Length > 0;

    public bool HasSecretError => _secretError.Length > 0;

    public string TestResult
    {
        get => _testResult;
        private set => SetField(ref _testResult, value);
    }

    public bool TestOk
    {
        get => _testOk;
        private set => SetField(ref _testOk, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsIdle => !_isBusy;

    public ICommand SaveCommand { get; }

    public ICommand TestCommand { get; }

    public ICommand CancelCommand { get; }

    public PresetFormValues Values => new(EditingId, Name, Url, ClientId, Secret);

    public void ShowErrors(IReadOnlyDictionary<FormField, string> errors)
    {
        NameError = errors.GetValueOrDefault(FormField.PresetName, string.Empty);
        UrlError = errors.GetValueOrDefault(FormField.OrchestratorUrl, string.Empty);
        ClientIdError = errors.GetValueOrDefault(FormField.ClientId, string.Empty);
        SecretError = errors.GetValueOrDefault(FormField.ClientSecret, string.Empty);
    }

    public void SetTestResult(bool ok, string message)
    {
        TestOk = ok;
        TestResult = message;
    }

    private void SetError(ref string field, string value, string flagName, [CallerMemberName] string? propertyName = null)
    {
        if (SetField(ref field, value, propertyName))
        {
            OnPropertyChanged(flagName);
        }
    }
}
