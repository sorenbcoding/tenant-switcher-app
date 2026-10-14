using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace SorenB.TenantSwitcher.Services;

/// <summary>
/// Applies the light or dark SorenB theme. Follows the Windows app theme unless the user
/// picked one in Settings, switches live when Windows changes, and keeps the title bar in step.
/// </summary>
public sealed class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DwmUseImmersiveDarkMode = 20;

    private static readonly Uri LightUri = new("pack://application:,,,/Themes/Semantic.Light.xaml");
    private static readonly Uri DarkUri = new("pack://application:,,,/Themes/Semantic.Dark.xaml");

    private ThemePreference _preference = ThemePreference.System;
    private ResourceDictionary? _current;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _preference == ThemePreference.System)
            {
                Application.Current?.Dispatcher.Invoke(Apply);
            }
        };
    }

    public bool IsDark { get; private set; }

    public void Apply(ThemePreference preference)
    {
        _preference = preference;
        Apply();
    }

    /// <summary>Sets the window's title bar to match the theme. Call once the window has a handle.</summary>
    public void UpdateTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = IsDark ? 1 : 0;
        // Fails harmlessly on Windows builds without immersive dark mode.
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private void Apply()
    {
        IsDark = _preference switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => !SystemUsesLightTheme(),
        };

        var resources = Application.Current.Resources.MergedDictionaries;
        var next = new ResourceDictionary { Source = IsDark ? DarkUri : LightUri };
        if (_current is not null)
        {
            resources.Remove(_current);
        }

        // Insert first so Controls.xaml (merged after it) resolves against it.
        resources.Insert(0, next);
        _current = next;

        foreach (Window window in Application.Current.Windows)
        {
            UpdateTitleBar(window);
        }
    }

    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
