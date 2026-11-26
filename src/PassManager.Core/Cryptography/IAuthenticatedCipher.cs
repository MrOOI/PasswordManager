namespace PassManager.Core.Cryptography;

/// <summary>Authenticated encryption with associated data (AEAD).</summary>
public interface IAuthenticatedCipher
{
    CipherAlgorithm Algorithm { get; }

    int KeySize { get; }

    /// <summary>Encrypts with a fresh random nonce.</summary>
    SealedData Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData);

    /// <summary>Decrypts and verifies integrity.</summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Authentication failed.</exception>
    void Decrypt(ReadOnlySpan<byte> key, SealedData sealedData, ReadOnlySpan<byte> associatedData, Span<byte> destination);
}
