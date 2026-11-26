namespace PassManager.Core.Settings;

/// <summary>User preferences. Contains no secrets and is stored unencrypted.</summary>
public sealed record AppSettings
{
    public const int MaxAutoLockMinutes = 24 * 60;
    public const int MaxClipboardClearSeconds = 600;

    /// <summary>Minutes of inactivity before the vault locks. 0 disables auto-lock.</summary>
    public int AutoLockMinutes { get; init; } = 5;

    /// <summary>Seconds before copied secrets are removed from the clipboard. 0 disables clearing.</summary>
    public int ClipboardClearSeconds { get; init; } = 20;

    public bool LockOnSessionLock { get; init; } = true;

    public bool LockOnMinimize { get; init; }

    /// <summary>Exclude application windows from screenshots, recordings and screen sharing.</summary>
    public bool HideFromScreenCapture { get; init; }

    public AppSettings Sanitize() => this with
    {
        AutoLockMinutes = Math.Clamp(AutoLockMinutes, 0, MaxAutoLockMinutes),
        ClipboardClearSeconds = Math.Clamp(ClipboardClearSeconds, 0, MaxClipboardClearSeconds),
    };
}

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Raised after settings were saved. May be raised on a background thread.</summary>
    event EventHandler<AppSettings>? SettingsChanged;

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
