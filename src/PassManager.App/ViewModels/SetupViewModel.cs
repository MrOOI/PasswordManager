using System.IO;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PassManager.Core.Passwords;
using PassManager.Core.Security;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>First-run experience: choose the master password and create the encrypted vault.</summary>
public sealed partial class SetupViewModel : ObservableObject, INavigationAware
{
    private readonly IVaultLifecycle _vault;
    private readonly IMasterPasswordPolicy _policy;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;
    private readonly ILogger<SetupViewModel> _logger;
    private SecureString? _password;
    private SecureString? _confirmation;

    [ObservableProperty]
    private PasswordStrengthResult? _strength;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateVaultCommand))]
    private bool _isBusy;

    public SetupViewModel(
        IVaultLifecycle vault,
        IMasterPasswordPolicy policy,
        IPasswordStrengthEvaluator strengthEvaluator,
        ILogger<SetupViewModel> logger)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _strengthEvaluator = strengthEvaluator ?? throw new ArgumentNullException(nameof(strengthEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SecureString? Password
    {
        get => _password;
        set
        {
            if (SecureInput.Replace(ref _password, value))
            {
                OnPropertyChanged();
                Strength = SecureInput.IsEmpty(_password) ? null : _password!.UseChars(chars => _strengthEvaluator.Evaluate(chars));
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

    public int MinimumLength => _policy.MinimumLength;

    public string VaultLocation => _vault.Location;

    public void OnNavigatedTo(object? parameter)
    {
    }

    public void OnNavigatedFrom()
    {
        SecureInput.Replace(ref _password, null);
        SecureInput.Replace(ref _confirmation, null);
    }

    private bool CanCreateVault() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCreateVault))]
    private async Task CreateVaultAsync()
    {
        ErrorMessage = null;
        if (_password is null || SecureInput.IsEmpty(_password))
        {
            ErrorMessage = "Choose a master password.";
            return;
        }

        MasterPasswordValidation validation = _password.UseChars(chars => _policy.Validate(chars));
        if (!validation.IsValid)
        {
            ErrorMessage = string.Join(Environment.NewLine, validation.Errors);
            return;
        }

        if (_confirmation is null || !_password.ContentEquals(_confirmation))
        {
            ErrorMessage = "The passwords don't match.";
            return;
        }

        IsBusy = true;
        try
        {
            using SensitiveBytes passwordBytes = _password.ToUtf8Bytes();
            await _vault.CreateAsync(passwordBytes);
        }
        catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Creating the vault failed.");
            ErrorMessage = $"The vault could not be created. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
