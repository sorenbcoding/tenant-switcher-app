using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using SorenB.TenantSwitcher.Models;
using SorenB.TenantSwitcher.Services;

namespace SorenB.TenantSwitcher.ViewModels;

public enum AppView
{
    Presets,
    Settings,
    About,
}

/// <summary>
/// The main window: banners, the add/edit form and the preset cards (same layout and flow as
/// the Studio extension's side panel), plus the Settings and About views.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    public const string RepositoryUrl = "https://github.com/sorenbcoding/tenant-switcher-app";

    private readonly PresetStore _store;
    private readonly AppStateStore _stateStore;
    private readonly TenantSwitchService _switcher;
    private readonly IdentityClient _identity;
    private readonly SwitchLog _log;
    private readonly AppState _state;

    private List<Preset> _presets = new();
    private AppView _view = AppView.Presets;
    private PresetFormViewModel? _form;
    private string _loadError = string.Empty;
    private bool _isSwitching;
    private string _switchTarget = string.Empty;
    private string _switchStep = string.Empty;
    private BannerViewModel? _notice;

    public MainViewModel(PresetStore store, AppStateStore stateStore, TenantSwitchService switcher, IdentityClient identity, SwitchLog log, ThemeService theme)
    {
        _store = store;
        _stateStore = stateStore;
        _switcher = switcher;
        _identity = identity;
        _log = log;
        _state = stateStore.LoadState();
        Settings = new SettingsViewModel(stateStore, theme);

        AddPresetCommand = new RelayCommand(_ => OpenForm(null), _ => !IsSwitching);
        ShowSettingsCommand = new RelayCommand(_ => View = View == AppView.Settings ? AppView.Presets : AppView.Settings);
        ShowAboutCommand = new RelayCommand(_ => View = View == AppView.About ? AppView.Presets : AppView.About);
        ShowPresetsCommand = new RelayCommand(_ => View = AppView.Presets);
        ShowLogCommand = new RelayCommand(_ => ShowLog());
        OpenRepositoryCommand = new RelayCommand(_ => OpenExternal(RepositoryUrl));

        _store.Changed += (_, _) => Application.Current?.Dispatcher.InvokeAsync(ReloadAsync);
        RefreshBanners();
    }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<PresetCardViewModel> Presets { get; } = new();

    public ObservableCollection<BannerViewModel> Banners { get; } = new();

    public AppView View
    {
        get => _view;
        set => SetField(ref _view, value);
    }

    public PresetFormViewModel? Form
    {
        get => _form;
        private set
        {
            if (SetField(ref _form, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string LoadError
    {
        get => _loadError;
        private set
        {
            if (SetField(ref _loadError, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool IsEmpty => _presets.Count == 0 && Form is null && LoadError.Length == 0;

    public bool IsSwitching
    {
        get => _isSwitching;
        private set
        {
            if (SetField(ref _isSwitching, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>True while a switch runs or credentials are being checked before one.</summary>
    public bool IsBusy => IsSwitching || Presets.Any(p => p.Prompt == PromptKind.Working);

    public string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? string.Empty : $"Version {version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public ICommand AddPresetCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand ShowAboutCommand { get; }

    public ICommand ShowPresetsCommand { get; }

    public ICommand ShowLogCommand { get; }

    public ICommand OpenRepositoryCommand { get; }

    public Task LoadAsync() => ReloadAsync();

    /// <summary>Re-reads presets.json and rebuilds the cards, keeping any open prompt.</summary>
    public async Task ReloadAsync()
    {
        try
        {
            _presets = await _store.LoadAsync();
            LoadError = string.Empty;
        }
        catch (InvalidDataException)
        {
            _presets = new List<Preset>();
            LoadError = $"Could not load presets — {_store.FilePath} is not valid JSON. Fix or remove the file; the list reloads when it changes.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _presets = new List<Preset>();
            LoadError = $"Could not load presets — {Reason(ex)}. Close any program that locks {_store.FilePath}; the list reloads when it changes.";
        }

        var prompts = Presets.Where(p => p.IsPromptOpen).ToDictionary(p => p.Id, p => (p.Prompt, p.PromptMessage));
        Presets.Clear();
        foreach (var preset in _presets.OrderBy(p => p.PresetName, StringComparer.CurrentCultureIgnoreCase))
        {
            var card = new PresetCardViewModel(this, preset, preset.Id == _state.ActivePresetId, SyncUipCli(preset.Id));
            if (prompts.TryGetValue(preset.Id, out var prompt))
            {
                card.SetPrompt(prompt.Prompt, prompt.PromptMessage);
            }

            Presets.Add(card);
        }

        OnPropertyChanged(nameof(IsEmpty));
        CommandManager.InvalidateRequerySuggested();
    }

    // ---- Cards ----

    internal void Connect(PresetCardViewModel card)
    {
        _notice = null;
        RefreshBanners();
        if (Settings.ConfirmBeforeSwitch)
        {
            OpenPrompt(card, PromptKind.Confirm);
        }
        else
        {
            _ = PreflightAndSwitchAsync(card);
        }
    }

    internal void Edit(PresetCardViewModel card)
    {
        View = AppView.Presets;
        OpenForm(card.Preset);
    }

    internal void OpenPrompt(PresetCardViewModel card, PromptKind kind, string message = "")
    {
        foreach (var other in Presets.Where(p => p != card && p.Prompt != PromptKind.Working))
        {
            other.SetPrompt(PromptKind.None);
        }

        card.SetPrompt(kind, message);
    }

    internal async Task ConfirmPromptAsync(PresetCardViewModel card)
    {
        switch (card.Prompt)
        {
            case PromptKind.Confirm:
                await PreflightAndSwitchAsync(card);
                break;
            case PromptKind.Delete:
                card.SetPrompt(PromptKind.None);
                await DeleteAsync(card);
                break;
            case PromptKind.Unverified:
                // "Switch anyway" skips the checks.
                card.SetPrompt(PromptKind.None);
                await SwitchAsync(card.Preset);
                break;
        }
    }

    private async Task PreflightAndSwitchAsync(PresetCardViewModel card)
    {
        OpenPrompt(card, PromptKind.Working, "Checking credentials…");
        Preflight preflight;
        try
        {
            preflight = await _switcher.PreflightAsync(card.Preset, Settings.Model);
        }
        catch (InvalidOperationException ex)
        {
            card.SetPrompt(PromptKind.None);
            ShowNotice(BannerSeverity.Error,
                $"Could not read the client secret for \"{card.Name}\" — {Reason(ex)}. Edit the preset and enter the client secret again.");
            return;
        }

        switch (preflight.Status)
        {
            case PreflightStatus.Blocked:
                card.SetPrompt(PromptKind.None);
                ShowNotice(BannerSeverity.Error, preflight.Message, preflight.OpenSettings);
                break;
            case PreflightStatus.Unverified:
                card.SetPrompt(PromptKind.Unverified, preflight.Message);
                break;
            default:
                card.SetPrompt(PromptKind.None);
                await SwitchAsync(card.Preset);
                break;
        }
    }

    private async Task SwitchAsync(Preset target)
    {
        var previous = _state.ActivePresetId is { } activeId && activeId != target.Id
            ? _presets.FirstOrDefault(p => p.Id == activeId)
            : null;
        var syncUip = SyncUipCli(target.Id) && OrchestratorUrl.GetTenantParts(target.OrchestratorUrl) is not null;

        _notice = null;
        _state.LastResult = null;
        _switchTarget = target.PresetName;
        _switchStep = string.Empty;
        IsSwitching = true;
        RefreshBanners();

        var progress = new Progress<string>(step =>
        {
            _switchStep = step;
            RefreshBanners();
        });
        var result = await _switcher.SwitchAsync(new SwitchRequest(target, previous, Settings.Model, syncUip), progress);

        if (result.Success)
        {
            _state.ActivePresetId = target.Id;
        }
        else if (!result.RolledBack)
        {
            _state.ActivePresetId = null;
        }

        _state.LastResult = result;
        SaveState();
        IsSwitching = false;
        foreach (var card in Presets)
        {
            card.IsActive = card.Id == _state.ActivePresetId;
        }

        RefreshBanners();
    }

    private async Task DeleteAsync(PresetCardViewModel card)
    {
        try
        {
            CredentialService.DeleteSecret(card.Id);
            await _store.RemoveAsync(card.Id);
            _state.Presets.Remove(card.Id);
            if (_state.ActivePresetId == card.Id)
            {
                _state.ActivePresetId = null;
            }

            SaveState();
            if (Form?.EditingId == card.Id)
            {
                Form = null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            ShowNotice(BannerSeverity.Error, $"Could not delete \"{card.Name}\" — {Reason(ex)}. Try again, or remove it from {_store.FilePath}.");
        }

        await ReloadAsync();
    }

    // ---- Form ----

    private void OpenForm(Preset? editing)
    {
        var hasStoredSecret = false;
        if (editing is not null)
        {
            try
            {
                hasStoredSecret = CredentialService.TryReadSecret(editing.Id) is { Length: > 0 };
            }
            catch (InvalidOperationException)
            {
                // Treat an unreadable secret as missing; the user can enter it again.
            }
        }

        Form = new PresetFormViewModel(this, editing, hasStoredSecret, editing is not null && SyncUipCli(editing.Id));
    }

    internal void CloseForm() => Form = null;

    internal async Task SaveFormAsync(PresetFormViewModel form)
    {
        form.IsBusy = true;
        try
        {
            var current = await _store.LoadAsync();
            var errors = PresetValidation.Validate(form.Values, current, form.HasStoredSecret);
            if (errors.Count > 0)
            {
                form.ShowErrors(errors);
                return;
            }

            var stored = await _store.UpsertAsync(new Preset
            {
                Id = form.EditingId ?? Guid.NewGuid(),
                PresetName = form.Name,
                OrchestratorUrl = form.Url,
                ClientId = form.ClientId,
            });
            if (form.Secret.Length > 0)
            {
                CredentialService.SaveSecret(stored.Id, form.Secret);
            }

            // uip login needs organization and tenant, so the option is off for standalone Orchestrator URLs.
            _state.Presets[stored.Id] = new PresetOptions
            {
                SyncUipCli = form.SyncUipCli && OrchestratorUrl.GetTenantParts(stored.OrchestratorUrl) is not null,
            };
            SaveState();
            Form = null;
            await ReloadAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            ShowNotice(BannerSeverity.Error,
                $"Could not save the preset — {Reason(ex)}. Check that {Path.GetDirectoryName(_store.FilePath)} is writable, then save again.");
        }
        finally
        {
            form.IsBusy = false;
        }
    }

    internal async Task TestFormAsync(PresetFormViewModel form)
    {
        form.IsBusy = true;
        form.SetTestResult(false, string.Empty);
        try
        {
            var secret = form.Secret;
            if (secret.Length == 0 && form.EditingId is { } id)
            {
                try
                {
                    secret = CredentialService.TryReadSecret(id) ?? string.Empty;
                }
                catch (InvalidOperationException)
                {
                    secret = string.Empty;
                }
            }

            if (secret.Length == 0)
            {
                form.SetTestResult(false, "Enter the client secret to test the credentials.");
                return;
            }

            var result = await _identity.ValidateAsync(form.Url.Trim(), form.ClientId.Trim(), secret);
            form.SetTestResult(
                result.Status == ValidationStatus.Ok,
                result.Status == ValidationStatus.Ok ? "Identity Server accepted the client ID and secret." : result.Message);
        }
        finally
        {
            form.IsBusy = false;
        }
    }

    // ---- Banners ----

    private void RefreshBanners()
    {
        Banners.Clear();
        if (IsSwitching)
        {
            Banners.Add(new BannerViewModel(BannerSeverity.Progress, $"Switching to {_switchTarget}",
                _switchStep.Length > 0 ? _switchStep + "…" : "Starting…"));
        }
        else if (_state.LastResult is { } result)
        {
            Banners.Add(new BannerViewModel(ToBanner(result.Severity), result.Message)
            {
                ShowLogCommand = ShowLogCommand,
                OkCommand = new RelayCommand(_ => DismissResult()),
            });
        }

        if (_notice is not null)
        {
            Banners.Add(_notice);
        }
    }

    private void ShowNotice(BannerSeverity severity, string message, bool openSettings = false)
    {
        _notice = new BannerViewModel(severity, message)
        {
            OpenSettingsCommand = openSettings ? new RelayCommand(_ => { DismissNotice(); View = AppView.Settings; }) : null,
            OkCommand = new RelayCommand(_ => DismissNotice()),
        };
        RefreshBanners();
    }

    private void DismissNotice()
    {
        _notice = null;
        RefreshBanners();
    }

    private void DismissResult()
    {
        _state.LastResult = null;
        SaveState();
        RefreshBanners();
    }

    private static BannerSeverity ToBanner(ResultSeverity severity) => severity switch
    {
        ResultSeverity.Error => BannerSeverity.Error,
        ResultSeverity.Warning => BannerSeverity.Warning,
        _ => BannerSeverity.Info,
    };

    // ---- Helpers ----

    private bool SyncUipCli(Guid id) => _state.Presets.TryGetValue(id, out var options) && options.SyncUipCli;

    private void SaveState()
    {
        try
        {
            _stateStore.SaveState(_state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowNotice(BannerSeverity.Warning,
                $"Could not save the app state — {Reason(ex)}. The connected preset and last result are not remembered after a restart.");
        }
    }

    private void ShowLog()
    {
        if (!File.Exists(_log.FilePath))
        {
            ShowNotice(BannerSeverity.Info, $"No switch has been logged yet. The log is written to {_log.FilePath} during the first switch.");
            return;
        }

        OpenExternal(_log.FilePath);
    }

    private void OpenExternal(string target)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ShowNotice(BannerSeverity.Error, $"Could not open {target} — {Reason(ex)}. Open it manually.");
        }
    }

    private static string Reason(Exception ex) => ex.Message.TrimEnd('.', ' ');
}
