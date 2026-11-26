using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PassManager.App.Converters;

/// <summary>Visible when the value is non-null (and non-empty for strings); inverted with <see cref="Invert"/>.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value is string text ? text.Length > 0 : value is not null;
        return hasValue ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
