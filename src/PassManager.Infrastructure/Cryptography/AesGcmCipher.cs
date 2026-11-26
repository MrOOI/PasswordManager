using System.Security.Cryptography;
using PassManager.Core.Cryptography;

namespace PassManager.Infrastructure.Cryptography;

/// <summary>
/// AES-256 in Galois/Counter Mode (NIST SP 800-38D): confidentiality plus integrity in one primitive,
/// hardware accelerated (AES-NI) through Windows CNG.
/// </summary>
public sealed class AesGcmCipher : IAuthenticatedCipher
{
    public const int KeySizeBytes = 32;
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;

    public CipherAlgorithm Algorithm => CipherAlgorithm.Aes256Gcm;

    public int KeySize => KeySizeBytes;

    public SealedData Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        ValidateKey(key);

        // 96-bit random nonces: safe for far more encryptions under one key than a vault will ever perform.
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return new SealedData(nonce, ciphertext, tag);
    }

    public void Decrypt(ReadOnlySpan<byte> key, SealedData sealedData, ReadOnlySpan<byte> associatedData, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(sealedData);
        ValidateKey(key);

        if (sealedData.Nonce.Length != NonceSizeBytes || sealedData.Tag.Length != TagSizeBytes)
        {
            throw new CryptographicException("Invalid nonce or authentication tag length.");
        }

        if (destination.Length != sealedData.Ciphertext.Length)
        {
            throw new ArgumentException("Destination length must equal the ciphertext length.", nameof(destination));
        }

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(sealedData.Nonce, sealedData.Ciphertext, sealedData.Tag, destination, associatedData);
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new CryptographicException($"AES-256-GCM requires a {KeySizeBytes}-byte key.");
        }
    }
}
