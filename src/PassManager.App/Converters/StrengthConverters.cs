using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PassManager.Core.Passwords;

namespace PassManager.App.Converters;

/// <summary>Maps a <see cref="PasswordStrengthResult"/> to a 0..5 meter value (0 = nothing entered).</summary>
public sealed class StrengthToMeterValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PasswordStrengthResult result ? (double)result.Strength + 1 : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a <see cref="PasswordStrengthResult"/> to its color on the strength scale.</summary>
public sealed class StrengthToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PasswordStrengthResult result)
        {
            return Brushes.Transparent;
        }

        string key = "Brush.Strength." + result.Strength;
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Human-readable summary, e.g. "Strong · 82 bits — Good password."</summary>
public sealed class StrengthToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PasswordStrengthResult result
            ? string.Create(CultureInfo.CurrentCulture, $"{result.Label} · ~{result.EntropyBits:0} bits — {result.Feedback}")
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
