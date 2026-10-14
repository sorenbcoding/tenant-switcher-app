using System.Windows.Input;
using SorenB.TenantSwitcher.Models;
using SorenB.TenantSwitcher.Services;

namespace SorenB.TenantSwitcher.ViewModels;

public enum PromptKind
{
    None,
    Confirm,
    Delete,
    Working,
    Unverified,
}

/// <summary>A preset card in the list, with its inline prompt (confirm, delete, working, unverified).</summary>
public sealed class PresetCardViewModel : ViewModelBase
{
    private readonly MainViewModel _owner;
    private bool _isActive;
    private PromptKind _prompt;
    private string _promptMessage = string.Empty;

    public PresetCardViewModel(MainViewModel owner, Preset preset, bool isActive, bool syncUipCli)
    {
        _owner = owner;
        Preset = preset;
        _isActive = isActive;
        SyncUipCli = syncUipCli;

        ConnectCommand = new RelayCommand(_ => _owner.Connect(this), _ => !IsActive && !_owner.IsBusy);
        EditCommand = new RelayCommand(_ => _owner.Edit(this), _ => !_owner.IsSwitching);
        DeleteCommand = new RelayCommand(_ => _owner.OpenPrompt(this, PromptKind.Delete), _ => !_owner.IsSwitching);
        PromptPrimaryCommand = new AsyncRelayCommand(_ => _owner.ConfirmPromptAsync(this));
        PromptCancelCommand = new RelayCommand(_ => SetPrompt(PromptKind.None));
    }

    public Preset Preset { get; }

    public Guid Id => Preset.Id;

    public string Name => string.IsNullOrWhiteSpace(Preset.PresetName) ? "(unnamed)" : Preset.PresetName;

    public string ShortUrl => OrchestratorUrl.ShortUrl(Preset.OrchestratorUrl);

    public bool SyncUipCli { get; }

    public string ToolTip =>
        $"{Preset.OrchestratorUrl}\nClient ID: {Preset.ClientId}" + (SyncUipCli ? "\nAlso signs in the uip CLI" : string.Empty);

    public string EditLabel => $"Edit {Name}";

    public string DeleteLabel => $"Delete {Name}";

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public PromptKind Prompt => _prompt;

    public bool IsPromptOpen => _prompt != PromptKind.None;

    /// <summary>Message passed with the prompt (working text, or why the credentials could not be verified).</summary>
    public string PromptMessage => _promptMessage;

    public string PromptQuestion => _prompt switch
    {
        PromptKind.Confirm => $"Do you want to switch tenant to {Name}?",
        PromptKind.Delete => $"Do you want to delete {Name}?",
        PromptKind.Unverified => $"Do you want to switch to {Name} anyway?",
        _ => string.Empty,
    };

    public string PromptDetail => _prompt switch
    {
        PromptKind.Confirm => "The Robot disconnects from the current tenant and connects to this one.",
        PromptKind.Delete => "The stored client secret is removed too. Tenant Switcher for UiPath Studio shares this preset.",
        PromptKind.Working => _promptMessage.Length > 0 ? _promptMessage : "Working…",
        PromptKind.Unverified => $"Could not verify the credentials. {_promptMessage}",
        _ => string.Empty,
    };

    public string PromptPrimaryText => _prompt switch
    {
        PromptKind.Delete => "Delete",
        PromptKind.Unverified => "Switch anyway",
        _ => "Switch",
    };

    public bool HasPromptButtons => _prompt is PromptKind.Confirm or PromptKind.Delete or PromptKind.Unverified;

    public ICommand ConnectCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand PromptPrimaryCommand { get; }

    public ICommand PromptCancelCommand { get; }

    public void SetPrompt(PromptKind kind, string message = "")
    {
        _prompt = kind;
        _promptMessage = message;
        OnPropertyChanged(nameof(Prompt));
        OnPropertyChanged(nameof(IsPromptOpen));
        OnPropertyChanged(nameof(PromptQuestion));
        OnPropertyChanged(nameof(PromptDetail));
        OnPropertyChanged(nameof(PromptPrimaryText));
        OnPropertyChanged(nameof(HasPromptButtons));
        CommandManager.InvalidateRequerySuggested();
    }
}
