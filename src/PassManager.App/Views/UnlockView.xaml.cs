using System.Windows;
using System.Windows.Controls;
using PassManager.App.ViewModels;

namespace PassManager.App.Views;

public partial class UnlockView : UserControl
{
    public UnlockView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is UnlockViewModel previous)
        {
            previous.UnlockFailed -= OnUnlockFailed;
        }

        if (e.NewValue is UnlockViewModel current)
        {
            current.UnlockFailed += OnUnlockFailed;
        }
    }

    // Pure view concern: after a failed attempt, select the text so the user can simply retype.
    private void OnUnlockFailed(object? sender, EventArgs e)
    {
        PasswordInput.Focus();
        PasswordInput.SelectAll();
    }
}
