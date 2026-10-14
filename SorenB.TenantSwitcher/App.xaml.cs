using System.Windows;
using System.Windows.Threading;
using SorenB.TenantSwitcher.Services;
using SorenB.TenantSwitcher.ViewModels;

namespace SorenB.TenantSwitcher;

public partial class App : Application
{
    private const string Caption = "Tenant Switcher for UiPath";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        var stateStore = new AppStateStore();
        var theme = new ThemeService();
        theme.Apply(stateStore.LoadSettings().Theme);

        var presetStore = new PresetStore();
        // Runs off the UI thread: blocking .GetAwaiter().GetResult() calls inside
        // MigrationService would otherwise deadlock against the WPF dispatcher.
        await Task.Run(() => MigrationService.TryMigrateLegacyPresets(AppContext.BaseDirectory, presetStore));

        var identity = new IdentityClient();
        var log = new SwitchLog();
        var switcher = new TenantSwitchService(identity, log);
        var robot = new RobotConnection();
        var viewModel = new MainViewModel(presetStore, stateStore, switcher, identity, log, robot, theme);
        await viewModel.LoadAsync();
        presetStore.StartWatching();
        robot.StartWatching();

        var window = new MainWindow(theme) { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }

    // Without these, an unhandled exception (e.g. during startup) closes the app with no
    // window and no dialog - indistinguishable from the exe simply failing to launch at all.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Tenant Switcher for UiPath hit an unexpected error — {e.Exception.Message} The app keeps running; if the problem repeats, restart it and report the details below at github.com/sorenbcoding/tenant-switcher-app.\n\n{e.Exception}",
            Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"Tenant Switcher for UiPath stopped — {ex.Message} Start it again; if the problem repeats, report the details below at github.com/sorenbcoding/tenant-switcher-app.\n\n{ex}",
                Caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
