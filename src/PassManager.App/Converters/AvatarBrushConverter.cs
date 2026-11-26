using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PassManager.App.Converters;

/// <summary>Picks a stable color for an entry's avatar from its title.</summary>
public sealed class AvatarBrushConverter : IValueConverter
{
    private static readonly Brush[] Palette =
    [
        Create("#2563EB"), Create("#7C3AED"), Create("#DB2777"), Create("#EA580C"),
        Create("#059669"), Create("#0891B2"), Create("#4F46E5"), Create("#CA8A04"),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value as string ?? string.Empty;
        int hash = 0;
        foreach (char c in text.ToUpperInvariant())
        {
            hash = unchecked((hash * 31) + c);
        }

        return Palette[(hash & int.MaxValue) % Palette.Length];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Create(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
