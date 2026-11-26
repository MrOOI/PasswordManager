using System.IO;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PassManager.Core.Passwords;
using PassManager.Core.Security;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>Verifies the current master password and re-keys the vault with a new one.</summary>
public sealed partial class ChangeMasterPasswordViewModel : DialogViewModelBase
{
    private readonly IVaultLifecycle _vault;
    private readonly IMasterPasswordPolicy _policy;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;
    private readonly ILogger<ChangeMasterPasswordViewModel> _logger;
    private SecureString? _currentPassword;
    private SecureString? _newPassword;
    private SecureString? _confirmation;

    [ObservableProperty]
    private PasswordStrengthResult? _strength;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeCommand))]
    private bool _isBusy;

    public ChangeMasterPasswordViewModel(
        IVaultLifecycle vault,
        IMasterPasswordPolicy policy,
        IPasswordStrengthEvaluator strengthEvaluator,
        ILogger<ChangeMasterPasswordViewModel> logger)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _strengthEvaluator = strengthEvaluator ?? throw new ArgumentNullException(nameof(strengthEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string WindowTitle => "Change master password";

    public SecureString? CurrentPassword
    {
        get => _currentPassword;
        set
        {
            if (SecureInput.Replace(ref _currentPassword, value))
            {
                OnPropertyChanged();
                ErrorMessage = null;
            }
        }
    }

    public SecureString? NewPassword
    {
        get => _newPassword;
        set
        {
            if (SecureInput.Replace(ref _newPassword, value))
            {
                OnPropertyChanged();
                Strength = SecureInput.IsEmpty(_newPassword) ? null : _newPassword!.UseChars(chars => _strengthEvaluator.Evaluate(chars));
                ErrorMessage = null;
            }
        }
    }

    public SecureString? Confirmation
    {
        get => _confirmation;
        set
        {
            if (SecureInput.Replace(ref _confirmation, value))
            {
                OnPropertyChanged();
                ErrorMessage = null;
            }
        }
    }

    public override void OnClosed()
    {
        SecureInput.Replace(ref _currentPassword, null);
        SecureInput.Replace(ref _newPassword, null);
        SecureInput.Replace(ref _confirmation, null);
    }

    private bool CanChange() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ChangeAsync()
    {
        ErrorMessage = null;
        if (_currentPassword is null || SecureInput.IsEmpty(_currentPassword))
        {
            ErrorMessage = "Enter your current master password.";
            return;
        }

        if (_newPassword is null || SecureInput.IsEmpty(_newPassword))
        {
            ErrorMessage = "Choose a new master password.";
            return;
        }

        MasterPasswordValidation validation = _newPassword.UseChars(chars => _policy.Validate(chars));
        if (!validation.IsValid)
        {
            ErrorMessage = string.Join(Environment.NewLine, validation.Errors);
            return;
        }

        if (_confirmation is null || !_newPassword.ContentEquals(_confirmation))
        {
            ErrorMessage = "The new passwords don't match.";
            return;
        }

        if (_newPassword.ContentEquals(_currentPassword))
        {
            ErrorMessage = "The new master password must be different from the current one.";
            return;
        }

        IsBusy = true;
        try
        {
            using SensitiveBytes current = _currentPassword.ToUtf8Bytes();
            using SensitiveBytes next = _newPassword.ToUtf8Bytes();
            await _vault.ChangeMasterPasswordAsync(current, next);
            RequestClose(true);
        }
        catch (InvalidMasterPasswordException)
        {
            ErrorMessage = "The current master password is incorrect.";
        }
        catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Changing the master password failed.");
            ErrorMessage = $"The master password could not be changed. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
