using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using PassManager.Core.Settings;
using PassManager.Core.Vault;

namespace PassManager.App.Services;

public interface IAutoLockService
{
    void Attach(Window mainWindow);
}

/// <summary>
/// Locks the vault automatically: after a period without keyboard/mouse activity in the application,
/// when Windows is locked or the user is switched, when the machine suspends and (optionally) on minimize.
/// </summary>
public sealed class AutoLockService : IAutoLockService, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly IVaultLifecycle _vault;
    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _timer;
    private Window? _window;
    private long _lastActivity;

    public AutoLockService(IVaultLifecycle vault, ISettingsService settings, IUiDispatcher dispatcher, TimeProvider timeProvider)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = CheckInterval };
        _timer.Tick += OnTimerTick;
    }

    public void Attach(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        if (_window is not null)
        {
            throw new InvalidOperationException("The auto-lock service is already attached.");
        }

        _window = mainWindow;
        _lastActivity = _timeProvider.GetTimestamp();

        InputManager.Current.PreProcessInput += OnPreProcessInput;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        mainWindow.StateChanged += OnWindowStateChanged;
        _vault.StateChanged += OnVaultStateChanged;
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _vault.StateChanged -= OnVaultStateChanged;
        if (_window is not null)
        {
            _window.StateChanged -= OnWindowStateChanged;
        }
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is KeyboardEventArgs or MouseEventArgs or TouchEventArgs or StylusEventArgs)
        {
            _lastActivity = _timeProvider.GetTimestamp();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        int minutes = _settings.Current.AutoLockMinutes;
        if (minutes > 0 && _vault.IsUnlocked && _timeProvider.GetElapsedTime(_lastActivity) >= TimeSpan.FromMinutes(minutes))
        {
            _vault.Lock(LockReason.Inactivity);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        bool leavingSession = e.Reason is SessionSwitchReason.SessionLock
            or SessionSwitchReason.ConsoleDisconnect
            or SessionSwitchReason.RemoteDisconnect
            or SessionSwitchReason.SessionLogoff;

        if (leavingSession && _settings.Current.LockOnSessionLock)
        {
            _dispatcher.Post(() => _vault.Lock(LockReason.SessionLocked));
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend && _settings.Current.LockOnSessionLock)
        {
            _dispatcher.Post(() => _vault.Lock(LockReason.SystemSuspend));
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window?.WindowState == WindowState.Minimized && _settings.Current.LockOnMinimize)
        {
            _vault.Lock(LockReason.Minimized);
        }
    }

    private void OnVaultStateChanged(object? sender, VaultStateChangedEventArgs e)
    {
        if (e.State == VaultState.Unlocked)
        {
            _lastActivity = _timeProvider.GetTimestamp();
        }
    }
}
