using System.Security.Cryptography;
using PassManager.Core.Cryptography;
using PassManager.Infrastructure.Cryptography;

namespace PassManager.Tests.Cryptography;

public sealed class AesGcmCipherTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(AesGcmCipher.KeySizeBytes);
    private static readonly byte[] AssociatedData = "context"u8.ToArray();
    private readonly AesGcmCipher _cipher = new();

    [Fact]
    public void Encrypt_then_decrypt_round_trips()
    {
        byte[] plaintext = "hello vault"u8.ToArray();

        SealedData sealedData = _cipher.Encrypt(Key, plaintext, AssociatedData);
        byte[] decrypted = new byte[sealedData.Ciphertext.Length];
        _cipher.Decrypt(Key, sealedData, AssociatedData, decrypted);

        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, sealedData.Ciphertext);
    }

    [Fact]
    public void Each_encryption_uses_a_fresh_nonce()
    {
        SealedData first = _cipher.Encrypt(Key, "same"u8, AssociatedData);
        SealedData second = _cipher.Encrypt(Key, "same"u8, AssociatedData);

        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        SealedData sealedData = _cipher.Encrypt(Key, "integrity matters"u8, AssociatedData);
        sealedData.Ciphertext[0] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() =>
            _cipher.Decrypt(Key, sealedData, AssociatedData, new byte[sealedData.Ciphertext.Length]));
    }

    [Fact]
    public void Different_associated_data_is_rejected()
    {
        SealedData sealedData = _cipher.Encrypt(Key, "bound to context"u8, AssociatedData);

        Assert.ThrowsAny<CryptographicException>(() =>
            _cipher.Decrypt(Key, sealedData, "other context"u8, new byte[sealedData.Ciphertext.Length]));
    }

    [Fact]
    public void Wrong_key_is_rejected()
    {
        SealedData sealedData = _cipher.Encrypt(Key, "secret"u8, AssociatedData);
        byte[] wrongKey = RandomNumberGenerator.GetBytes(AesGcmCipher.KeySizeBytes);

        Assert.ThrowsAny<CryptographicException>(() =>
            _cipher.Decrypt(wrongKey, sealedData, AssociatedData, new byte[sealedData.Ciphertext.Length]));
    }

    [Fact]
    public void Key_of_wrong_size_is_rejected()
    {
        Assert.ThrowsAny<CryptographicException>(() => _cipher.Encrypt(new byte[16], "x"u8, AssociatedData));
    }
}
