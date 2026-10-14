using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SorenB.TenantSwitcher.Services;

namespace SorenB.TenantSwitcher.ViewModels;

/// <summary>The Settings view. Every change is saved straight away.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppStateStore _store;
    private readonly ThemeService _theme;
    private string _saveError = string.Empty;

    public SettingsViewModel(AppStateStore store, ThemeService theme)
    {
        _store = store;
        _theme = theme;
        Model = store.LoadSettings();
        BrowseUiRobotCommand = new RelayCommand(_ => BrowseUiRobot());
    }

    public AppSettings Model { get; }

    public bool ThemeSystem
    {
        get => Model.Theme == ThemePreference.System;
        set => SetTheme(value, ThemePreference.System);
    }

    public bool ThemeLight
    {
        get => Model.Theme == ThemePreference.Light;
        set => SetTheme(value, ThemePreference.Light);
    }

    public bool ThemeDark
    {
        get => Model.Theme == ThemePreference.Dark;
        set => SetTheme(value, ThemePreference.Dark);
    }

    public bool ConfirmBeforeSwitch
    {
        get => Model.ConfirmBeforeSwitch;
        set => Update(() => Model.ConfirmBeforeSwitch = value);
    }

    public bool ValidateBeforeSwitch
    {
        get => Model.ValidateBeforeSwitch;
        set => Update(() => Model.ValidateBeforeSwitch = value);
    }

    public bool RestartAssistant
    {
        get => Model.RestartAssistant;
        set => Update(() => Model.RestartAssistant = value);
    }

    public string UiRobotPath
    {
        get => Model.UiRobotPath;
        set
        {
            Update(() => Model.UiRobotPath = (value ?? string.Empty).Trim().Trim('"'));
            OnPropertyChanged(nameof(UiRobotStatus));
            OnPropertyChanged(nameof(UiRobotPathInvalid));
        }
    }

    public string UipCliPath
    {
        get => Model.UipCliPath;
        set => Update(() => Model.UipCliPath = string.IsNullOrWhiteSpace(value) ? "uip" : value.Trim().Trim('"'));
    }

    public bool UiRobotPathInvalid => Model.UiRobotPath.Length > 0 && !File.Exists(Model.UiRobotPath);

    public string UiRobotStatus
    {
        get
        {
            if (Model.UiRobotPath.Length > 0)
            {
                return File.Exists(Model.UiRobotPath)
                    ? "Uses this file instead of searching the default locations."
                    : "The file does not exist. Enter the full path of UiRobot.exe, or clear the field to search the default locations.";
            }

            var found = UiPathLocator.FindUiRobotExe();
            return found is not null
                ? $"Found automatically: {found}"
                : "UiRobot.exe was not found in the default locations. Enter its full path.";
        }
    }

    public string SaveError
    {
        get => _saveError;
        private set => SetField(ref _saveError, value);
    }

    public string PresetsPath => PresetStore.GetDefaultFilePath();

    public string AppDataPath => AppStateStore.DefaultDirectory;

    public ICommand BrowseUiRobotCommand { get; }

    private void SetTheme(bool selected, ThemePreference theme)
    {
        if (!selected || Model.Theme == theme)
        {
            return;
        }

        Update(() => Model.Theme = theme, nameof(ThemeSystem));
        OnPropertyChanged(nameof(ThemeLight));
        OnPropertyChanged(nameof(ThemeDark));
        _theme.Apply(theme);
    }

    private void Update(Action change, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        change();
        OnPropertyChanged(propertyName);
        try
        {
            _store.SaveSettings(Model);
            SaveError = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = $"Could not save the settings — {ex.Message.TrimEnd('.')}. Check that {_store.Directory} is writable.";
        }
    }

    private void BrowseUiRobot()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select UiRobot.exe",
            Filter = "UiRobot.exe|UiRobot.exe|Programs (*.exe)|*.exe",
            CheckFileExists = true,
        };
        var current = Model.UiRobotPath.Length > 0 ? Model.UiRobotPath : UiPathLocator.FindUiRobotExe();
        if (current is not null && Directory.Exists(Path.GetDirectoryName(current)))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(current);
        }

        if (dialog.ShowDialog() == true)
        {
            UiRobotPath = dialog.FileName;
        }
    }
}
