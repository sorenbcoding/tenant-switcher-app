using System.Windows;
using OrchestratorConnector.Services;
using OrchestratorConnector.ViewModels;

namespace OrchestratorConnector;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var presetStore = new PresetStore();
        MigrationService.TryMigrateLegacyPresets(AppContext.BaseDirectory, presetStore);

        var switcher = new OrchestratorSwitcher();
        var viewModel = new MainViewModel(presetStore, switcher);
        viewModel.LoadAsync().GetAwaiter().GetResult();

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();
    }
}
