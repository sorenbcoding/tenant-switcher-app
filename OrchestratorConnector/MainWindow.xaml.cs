using System.ComponentModel;
using System.Windows;
using OrchestratorConnector.ViewModels;

namespace OrchestratorConnector;

public partial class MainWindow : Window
{
    private bool _suppressPasswordSync;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    // PasswordBox.Password can't be data-bound (by design), so it's wired directly here
    // instead of through a binding - a plain CLR event subscription that's guaranteed to
    // fire on every keystroke, with none of the timing pitfalls of the attached-property
    // binding approach this replaced (which silently never subscribed its own event
    // handler in the common case where the field starts and resets to an empty string).
    private void ClientSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _suppressPasswordSync = true;
        viewModel.EditClientSecret = ClientSecretBox.Password;
        _suppressPasswordSync = false;
    }

    // Keeps the visible PasswordBox in sync when the ViewModel clears EditClientSecret
    // itself (after Save, after Delete, when loading a different preset into the form).
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressPasswordSync || e.PropertyName != nameof(MainViewModel.EditClientSecret))
        {
            return;
        }

        var viewModel = (MainViewModel)sender!;
        if (ClientSecretBox.Password != viewModel.EditClientSecret)
        {
            ClientSecretBox.Password = viewModel.EditClientSecret;
        }
    }
}
