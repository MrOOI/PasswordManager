using CommunityToolkit.Mvvm.Input;

namespace PassManager.App.ViewModels;

/// <summary>Styled replacement for MessageBox (confirmations and errors).</summary>
public sealed partial class MessageDialogViewModel : DialogViewModelBase
{
    public MessageDialogViewModel(string title, string message, string confirmText, string? cancelText, bool isDestructive)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDestructive = isDestructive;
    }

    public override string WindowTitle => Title;

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public string? CancelText { get; }

    public bool HasCancel => CancelText is not null;

    public bool IsDestructive { get; }

    public string Glyph => IsDestructive ? "" : HasCancel ? "" : "";

    [RelayCommand]
    private void Confirm() => RequestClose(true);

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
