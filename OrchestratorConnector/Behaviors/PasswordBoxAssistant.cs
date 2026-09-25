using System.Windows;
using System.Windows.Controls;

namespace OrchestratorConnector.Behaviors;

/// <summary>
/// PasswordBox.Password can't be data-bound directly (by design). This attached property
/// mirrors it into a bindable string so the ViewModel never needs code-behind access to
/// the control.
/// </summary>
public static class PasswordBoxAssistant
{
    public static readonly DependencyProperty BoundPassword = DependencyProperty.RegisterAttached(
        "BoundPassword",
        typeof(string),
        typeof(PasswordBoxAssistant),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdating = DependencyProperty.RegisterAttached(
        "IsUpdating",
        typeof(bool),
        typeof(PasswordBoxAssistant));

    public static string GetBoundPassword(DependencyObject obj) => (string)obj.GetValue(BoundPassword);

    public static void SetBoundPassword(DependencyObject obj, string value) => obj.SetValue(BoundPassword, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= HandlePasswordChanged;

        if (!(bool)passwordBox.GetValue(IsUpdating))
        {
            passwordBox.Password = e.NewValue as string ?? string.Empty;
        }

        passwordBox.PasswordChanged += HandlePasswordChanged;
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        var passwordBox = (PasswordBox)sender;
        passwordBox.SetValue(IsUpdating, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        // SetValue alone does not reliably push through to a TwoWay binding's source in
        // every scenario - force it explicitly rather than relying on that happening implicitly.
        passwordBox.GetBindingExpression(BoundPassword)?.UpdateSource();
        passwordBox.SetValue(IsUpdating, false);
    }
}
