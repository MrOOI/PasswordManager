namespace PassManager.App.Composition;

/// <summary>
/// Ensures only one instance runs per Windows session (two instances writing the same vault would race).
/// A second launch signals the first one to come to the foreground and then exits.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>Also referenced by the installer (AppMutex) to detect a running instance.</summary>
    public const string MutexName = "PassManager_7F3A2C1E5B4D4E8A9C6F2D1B0A9E8F7C";
    private const string ActivationEventName = "PassManager_7F3A2C1E5B4D4E8A9C6F2D1B0A9E8F7C_Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private RegisteredWaitHandle? _registration;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activationEvent, bool isFirstInstance)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
        IsFirstInstance = isFirstInstance;
    }

    public bool IsFirstInstance { get; }

    public static SingleInstanceGuard Acquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        return new SingleInstanceGuard(mutex, activationEvent, createdNew);
    }

    public void SignalFirstInstance() => _activationEvent.Set();

    public void ListenForActivation(Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent, (_, _) => onActivationRequested(), state: null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activationEvent.Dispose();
    }
}
