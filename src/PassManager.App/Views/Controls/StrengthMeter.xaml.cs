using System.Windows;
using System.Windows.Controls;
using PassManager.Core.Passwords;

namespace PassManager.App.Views.Controls;

/// <summary>Colored bar plus summary text for a <see cref="PasswordStrengthResult"/>.</summary>
public partial class StrengthMeter : UserControl
{
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
        nameof(Result), typeof(PasswordStrengthResult), typeof(StrengthMeter), new PropertyMetadata(null));

    public StrengthMeter()
    {
        InitializeComponent();
    }

    public PasswordStrengthResult? Result
    {
        get => (PasswordStrengthResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }
}
