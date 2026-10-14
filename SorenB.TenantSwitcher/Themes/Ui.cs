using System.Windows;
using System.Windows.Controls;

namespace SorenB.TenantSwitcher.Themes;

/// <summary>Attached properties used by the control styles in Controls.xaml.</summary>
public static class Ui
{
    /// <summary>Marks an input as invalid; the style draws its border in App.Error.</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    /// <summary>Muted text shown inside an empty TextBox or PasswordBox.</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(Ui), new PropertyMetadata(string.Empty));

    /// <summary>Focuses the element when it is loaded (used for the first form field).</summary>
    public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.RegisterAttached(
        "FocusOnLoad", typeof(bool), typeof(Ui), new PropertyMetadata(false, OnFocusOnLoadChanged));

    /// <summary>
    /// Bindable PasswordBox.Password. PasswordBox does not expose Password as a dependency
    /// property, so this keeps the two in sync in both directions.
    /// </summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password", typeof(string), typeof(Ui),
        // Default null (not "") so binding an empty string still fires the callback that hooks the box.
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    /// <summary>True while a PasswordBox is empty; drives the placeholder.</summary>
    public static readonly DependencyProperty PasswordEmptyProperty = DependencyProperty.RegisterAttached(
        "PasswordEmpty", typeof(bool), typeof(Ui), new PropertyMetadata(true));

    private static readonly DependencyProperty IsSyncingProperty = DependencyProperty.RegisterAttached(
        "IsSyncing", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    private static readonly DependencyProperty IsHookedProperty = DependencyProperty.RegisterAttached(
        "IsHooked", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static bool GetHasError(DependencyObject d) => (bool)d.GetValue(HasErrorProperty);

    public static void SetHasError(DependencyObject d, bool value) => d.SetValue(HasErrorProperty, value);

    public static string GetPlaceholder(DependencyObject d) => (string)d.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject d, string value) => d.SetValue(PlaceholderProperty, value);

    public static bool GetFocusOnLoad(DependencyObject d) => (bool)d.GetValue(FocusOnLoadProperty);

    public static void SetFocusOnLoad(DependencyObject d, bool value) => d.SetValue(FocusOnLoadProperty, value);

    public static string GetPassword(DependencyObject d) => (string?)d.GetValue(PasswordProperty) ?? string.Empty;

    public static void SetPassword(DependencyObject d, string value) => d.SetValue(PasswordProperty, value);

    public static bool GetPasswordEmpty(DependencyObject d) => (bool)d.GetValue(PasswordEmptyProperty);

    public static void SetPasswordEmpty(DependencyObject d, bool value) => d.SetValue(PasswordEmptyProperty, value);

    private static void OnFocusOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is true)
        {
            element.Loaded += (_, _) => element.Focus();
        }
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        if (!(bool)box.GetValue(IsHookedProperty))
        {
            box.SetValue(IsHookedProperty, true);
            box.PasswordChanged += OnPasswordBoxChanged;
        }

        if ((bool)box.GetValue(IsSyncingProperty))
        {
            return;
        }

        var value = e.NewValue as string ?? string.Empty;
        if (box.Password != value)
        {
            box.Password = value;
        }

        SetPasswordEmpty(box, value.Length == 0);
    }

    private static void OnPasswordBoxChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(IsSyncingProperty, true);
        try
        {
            SetPassword(box, box.Password);
            SetPasswordEmpty(box, box.Password.Length == 0);
        }
        finally
        {
            box.SetValue(IsSyncingProperty, false);
        }
    }
}
