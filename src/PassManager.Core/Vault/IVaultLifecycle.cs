using PassManager.Core.Security;

namespace PassManager.Core.Vault;

/// <summary>Creating, unlocking, locking and re-keying the vault.</summary>
public interface IVaultLifecycle
{
    VaultState State { get; }

    bool IsUnlocked { get; }

    /// <summary>Human-readable location of the vault (e.g. a file path).</summary>
    string Location { get; }

    /// <summary>Raised after the state changes. May be raised on a background thread.</summary>
    event EventHandler<VaultStateChangedEventArgs>? StateChanged;

    Task CreateAsync(SensitiveBytes masterPassword, CancellationToken cancellationToken = default);

    Task UnlockAsync(SensitiveBytes masterPassword, CancellationToken cancellationToken = default);

    /// <summary>Verifies the current password, then re-encrypts the vault under a brand-new key hierarchy.</summary>
    Task ChangeMasterPasswordAsync(SensitiveBytes currentPassword, SensitiveBytes newPassword, CancellationToken cancellationToken = default);

    /// <summary>Wipes keys and decrypted data from memory.</summary>
    void Lock(LockReason reason);
}
