using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PassManager.App.Helpers;

/// <summary>
/// MVVM support for <see cref="PasswordBox"/>, which deliberately does not expose a bindable password.
/// <list type="bullet">
/// <item><c>SecurePassword</c>: pushes a <see cref="SecureString"/> copy to the view model (master passwords never become strings).</item>
/// <item><c>BoundPassword</c>: two-way string binding, used only for entry passwords that are stored as text anyway.</item>
/// <item><c>HasPassword</c>: read-only flag used by templates to show a placeholder.</item>
/// </list>
/// </summary>
public static class PasswordBoxAssist
{
    public static readonly DependencyProperty TrackPasswordProperty = DependencyProperty.RegisterAttached(
        "TrackPassword", typeof(bool), typeof(PasswordBoxAssist), new PropertyMetadata(false, OnTrackPasswordChanged));

    public static readonly DependencyProperty SecurePasswordProperty = DependencyProperty.RegisterAttached(
        "SecurePassword", typeof(SecureString), typeof(PasswordBoxAssist), new PropertyMetadata(null));

    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword",
        typeof(string),
        typeof(PasswordBoxAssist),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyPropertyKey HasPasswordPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "HasPassword", typeof(bool), typeof(PasswordBoxAssist), new PropertyMetadata(false));

    public static readonly DependencyProperty HasPasswordProperty = HasPasswordPropertyKey.DependencyProperty;

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating", typeof(bool), typeof(PasswordBoxAssist), new PropertyMetadata(false));

    public static bool GetTrackPassword(DependencyObject element) => (bool)element.GetValue(TrackPasswordProperty);

    public static void SetTrackPassword(DependencyObject element, bool value) => element.SetValue(TrackPasswordProperty, value);

    public static SecureString? GetSecurePassword(DependencyObject element) => (SecureString?)element.GetValue(SecurePasswordProperty);

    public static void SetSecurePassword(DependencyObject element, SecureString? value) => element.SetValue(SecurePasswordProperty, value);

    public static string GetBoundPassword(DependencyObject element) => (string)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string value) => element.SetValue(BoundPasswordProperty, value);

    public static bool GetHasPassword(DependencyObject element) => (bool)element.GetValue(HasPasswordProperty);

    private static void OnTrackPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue)
        {
            box.PasswordChanged += OnPasswordChanged;
            UpdateHasPassword(box);
        }
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        UpdateHasPassword(box);

        if (BindingOperations.IsDataBound(box, SecurePasswordProperty))
        {
            // PasswordBox.SecurePassword returns a fresh copy; the view model owns and disposes it.
            box.SetCurrentValue(SecurePasswordProperty, box.SecurePassword);
        }

        if (!(bool)box.GetValue(IsUpdatingProperty) && BindingOperations.IsDataBound(box, BoundPasswordProperty))
        {
            box.SetValue(IsUpdatingProperty, true);
            try
            {
                box.SetCurrentValue(BoundPasswordProperty, box.Password);
            }
            finally
            {
                box.SetValue(IsUpdatingProperty, false);
            }
        }
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box || (bool)box.GetValue(IsUpdatingProperty))
        {
            return;
        }

        string value = e.NewValue as string ?? string.Empty;
        if (box.Password == value)
        {
            return;
        }

        box.SetValue(IsUpdatingProperty, true);
        try
        {
            box.Password = value;
        }
        finally
        {
            box.SetValue(IsUpdatingProperty, false);
        }

        UpdateHasPassword(box);
    }

    private static void UpdateHasPassword(PasswordBox box)
    {
        using SecureString copy = box.SecurePassword;
        box.SetValue(HasPasswordPropertyKey, copy.Length > 0);
    }
}
