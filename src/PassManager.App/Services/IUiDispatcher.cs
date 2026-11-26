using System.Windows.Threading;

namespace PassManager.App.Services;

/// <summary>Marshals work onto the UI thread.</summary>
public interface IUiDispatcher
{
    /// <summary>Runs inline when already on the UI thread; otherwise queues asynchronously.</summary>
    void Post(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfUiDispatcher()
    {
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }
}
