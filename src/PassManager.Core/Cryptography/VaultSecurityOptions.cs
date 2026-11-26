namespace PassManager.Core.Cryptography;

/// <summary>
/// Cryptographic policy for new or re-keyed vaults. Defaults follow current OWASP guidance for
/// Argon2id (and match the defaults of mainstream password managers).
/// </summary>
public sealed class VaultSecurityOptions
{
    public static VaultSecurityOptions Default { get; } = new();

    public CipherAlgorithm Cipher { get; init; } = CipherAlgorithm.Aes256Gcm;

    public KdfAlgorithm Kdf { get; init; } = KdfAlgorithm.Argon2id;

    /// <summary>Argon2 memory cost in KiB (64 MiB).</summary>
    public int MemoryKiB { get; init; } = 64 * 1024;

    public int Iterations { get; init; } = 3;

    public int Parallelism { get; init; } = 4;

    public int SaltSize { get; init; } = 32;
}
