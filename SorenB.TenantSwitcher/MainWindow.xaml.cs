using System.Windows;
using SorenB.TenantSwitcher.Services;

namespace SorenB.TenantSwitcher;

public partial class MainWindow : Window
{
    public MainWindow(ThemeService theme)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => theme.UpdateTitleBar(this);
    }
}
