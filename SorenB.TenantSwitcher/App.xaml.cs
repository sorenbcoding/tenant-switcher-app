using System.Windows;
using System.Windows.Threading;
using SorenB.TenantSwitcher.Services;
using SorenB.TenantSwitcher.ViewModels;

namespace SorenB.TenantSwitcher;

public partial class App : Application
{
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

        var switcher = new TenantSwitchService(new IdentityClient(), new SwitchLog());
        var viewModel = new MainViewModel(presetStore, switcher);
        await viewModel.LoadAsync();

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }

    // Without these, an unhandled exception (e.g. during startup) closes the app with no
    // window and no dialog - indistinguishable from the exe simply failing to launch at all.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Tenant Switcher for UiPath - Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Tenant Switcher for UiPath - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
