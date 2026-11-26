using CommunityToolkit.Mvvm.ComponentModel;

namespace PassManager.App.ViewModels;

/// <summary>
/// Lifecycle hooks for page view models. Used instead of IDisposable so that transient view models are
/// not tracked (and kept alive) by the DI container.
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo(object? parameter);

    /// <summary>Release secrets and subscriptions; the page is being discarded.</summary>
    void OnNavigatedFrom();
}

public sealed class DialogCloseRequestedEventArgs(bool? result) : EventArgs
{
    public bool? Result { get; } = result;
}

/// <summary>Base class for view models shown in modal dialog windows.</summary>
public abstract class DialogViewModelBase : ObservableObject
{
    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    public abstract string WindowTitle { get; }

    /// <summary>Called by the dialog service after the window has closed, for any reason.</summary>
    public virtual void OnClosed()
    {
    }

    protected void RequestClose(bool? result) => CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(result));
}
