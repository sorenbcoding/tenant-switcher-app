using System.Windows;
using OrchestratorConnector.Services;
using OrchestratorConnector.ViewModels;

namespace OrchestratorConnector;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var presetStore = new PresetStore();
        // Runs off the UI thread: blocking .GetAwaiter().GetResult() calls inside
        // MigrationService would otherwise deadlock against the WPF dispatcher.
        await Task.Run(() => MigrationService.TryMigrateLegacyPresets(AppContext.BaseDirectory, presetStore));

        var switcher = new OrchestratorSwitcher();
        var viewModel = new MainViewModel(presetStore, switcher);
        await viewModel.LoadAsync();

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }
}
