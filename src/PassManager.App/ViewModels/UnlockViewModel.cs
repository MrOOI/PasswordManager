using System.IO;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PassManager.Core.Security;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>
/// Unlock screen. Repeated failures add a growing cool-down. The real brute-force protection is the
/// Argon2id cost (which also applies offline); this only slows down guessing at an unattended machine.
/// </summary>
public sealed partial class UnlockViewModel : ObservableObject, INavigationAware
{
    private const int FreeAttempts = 3;
    private const int MaxCooldownSeconds = 60;

    private readonly IVaultLifecycle _vault;
    private readonly ILogger<UnlockViewModel> _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private SecureString? _password;
    private int _failedAttempts;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _infoMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    [NotifyPropertyChangedFor(nameof(IsCoolingDown))]
    private int _cooldownSecondsRemaining;

    public UnlockViewModel(IVaultLifecycle vault, ILogger<UnlockViewModel> logger)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Raised after a failed attempt so the view can re-select the password text.</summary>
    public event EventHandler? UnlockFailed;

    public SecureString? Password
    {
        get => _password;
        set
        {
            if (SecureInput.Replace(ref _password, value))
            {
                OnPropertyChanged();
                ErrorMessage = null;
            }
        }
    }

    public bool IsCoolingDown => CooldownSecondsRemaining > 0;

    public string VaultLocation => _vault.Location;

    public void OnNavigatedTo(object? parameter)
    {
        InfoMessage = parameter switch
        {
            LockReason.Inactivity => "Locked automatically after a period of inactivity.",
            LockReason.SessionLocked => "Locked because Windows was locked.",
            LockReason.SystemSuspend => "Locked because the computer went to sleep.",
            LockReason.Minimized => "Locked because the window was minimized.",
            _ => null,
        };
    }

    public void OnNavigatedFrom()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        SecureInput.Replace(ref _password, null);
    }

    private bool CanUnlock() => !IsBusy && !IsCoolingDown;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        ErrorMessage = null;
        if (_password is null || SecureInput.IsEmpty(_password))
        {
            ErrorMessage = "Enter your master password.";
            return;
        }

        bool failed = false;
        IsBusy = true;
        try
        {
            using SensitiveBytes passwordBytes = _password.ToUtf8Bytes();
            await _vault.UnlockAsync(passwordBytes);
            _failedAttempts = 0;
        }
        catch (InvalidMasterPasswordException)
        {
            failed = true;
            _failedAttempts++;
            ErrorMessage = "Incorrect master password. Please try again.";
        }
        catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Unlocking the vault failed.");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        if (failed)
        {
            UnlockFailed?.Invoke(this, EventArgs.Empty);
            await CooldownAsync();
        }
    }

    private async Task CooldownAsync()
    {
        if (_failedAttempts < FreeAttempts)
        {
            return;
        }

        int seconds = Math.Min(MaxCooldownSeconds, 5 * (_failedAttempts - FreeAttempts + 1));
        try
        {
            for (int remaining = seconds; remaining > 0; remaining--)
            {
                CooldownSecondsRemaining = remaining;
                await Task.Delay(TimeSpan.FromSeconds(1), _lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            CooldownSecondsRemaining = 0;
        }
    }
}
