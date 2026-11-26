using PassManager.Core.Security;

namespace PassManager.Core.Cryptography;

/// <summary>A freshly generated data key together with the header that wraps it.</summary>
public sealed record VaultKeyMaterial(ProtectedKey Key, VaultHeader Header);

/// <summary>
/// Implements the vault key hierarchy:
/// master password → (KDF) → key-encryption key → unwraps → random data key → encrypts payload.
/// </summary>
public interface IVaultCryptoService
{
    /// <summary>Generates a new random data key and wraps it with a key derived from <paramref name="masterPassword"/>.</summary>
    VaultKeyMaterial CreateKeyMaterial(ReadOnlySpan<byte> masterPassword);

    /// <summary>Derives the key-encryption key and unwraps the data key.</summary>
    /// <exception cref="Vault.InvalidMasterPasswordException">Wrong password or tampered header.</exception>
    ProtectedKey UnlockKey(ReadOnlySpan<byte> masterPassword, VaultHeader header);

    /// <summary>True when the header uses weaker parameters than the current security policy.</summary>
    bool RequiresUpgrade(VaultHeader header);

    SealedData EncryptPayload(ProtectedKey key, VaultHeader header, ReadOnlySpan<byte> plaintext);

    /// <exception cref="Vault.VaultCorruptedException">Payload failed integrity verification.</exception>
    SensitiveBytes DecryptPayload(ProtectedKey key, VaultHeader header, SealedData payload);
}
