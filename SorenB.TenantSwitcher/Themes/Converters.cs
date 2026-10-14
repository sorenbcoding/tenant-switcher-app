using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SorenB.TenantSwitcher.Themes;

/// <summary>
/// Visible when the value is "set": true, a non-null object, a non-empty string, or equal to
/// ConverterParameter when one is given. Invert="True" flips the result.
/// </summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = parameter is not null
            ? string.Equals(value?.ToString(), parameter.ToString(), StringComparison.Ordinal)
            : value switch
            {
                null => false,
                bool b => b,
                string s => s.Length > 0,
                _ => true,
            };
        return visible != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
