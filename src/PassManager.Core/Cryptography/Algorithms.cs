namespace PassManager.Core.Cryptography;

/// <summary>Key derivation algorithms. Values are persisted in the vault header: never renumber.</summary>
public enum KdfAlgorithm : byte
{
    Argon2id = 1,
}

/// <summary>Authenticated encryption algorithms. Values are persisted in the vault header: never renumber.</summary>
public enum CipherAlgorithm : byte
{
    Aes256Gcm = 1,
}
