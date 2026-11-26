using PassManager.Core.Cryptography;
using PassManager.Core.Security;
using PassManager.Core.Vault;
using PassManager.Tests.TestSupport;

namespace PassManager.Tests.Cryptography;

public sealed class VaultCryptoServiceTests
{
    private static readonly byte[] Password = "correct horse battery staple"u8.ToArray();
    private readonly VaultCryptoService _crypto = TestFactory.CreateCryptoService();

    [Fact]
    public void Unlocking_with_the_correct_password_recovers_the_data_key()
    {
        VaultKeyMaterial material = _crypto.CreateKeyMaterial(Password);
        SealedData payload = _crypto.EncryptPayload(material.Key, material.Header, "top secret"u8);

        using ProtectedKey unlocked = _crypto.UnlockKey(Password, material.Header);
        using SensitiveBytes plaintext = _crypto.DecryptPayload(unlocked, material.Header, payload);

        Assert.Equal("top secret"u8.ToArray(), plaintext.ReadOnlySpan.ToArray());
    }

    [Fact]
    public void Unlocking_with_a_wrong_password_fails()
    {
        VaultKeyMaterial material = _crypto.CreateKeyMaterial(Password);

        Assert.Throws<InvalidMasterPasswordException>(() => _crypto.UnlockKey("wrong password"u8, material.Header));
    }

    [Fact]
    public void Tampering_with_kdf_parameters_is_detected()
    {
        VaultKeyMaterial material = _crypto.CreateKeyMaterial(Password);
        KdfParameters original = material.Header.Kdf;
        VaultHeader downgraded = material.Header with
        {
            Kdf = new KdfParameters(original.Algorithm, original.Salt, original.MemoryKiB, original.Iterations + 1, original.Parallelism),
        };

        Assert.Throws<InvalidMasterPasswordException>(() => _crypto.UnlockKey(Password, downgraded));
    }

    [Fact]
    public void Tampered_payload_is_reported_as_corruption()
    {
        VaultKeyMaterial material = _crypto.CreateKeyMaterial(Password);
        SealedData payload = _crypto.EncryptPayload(material.Key, material.Header, "data"u8);
        payload.Ciphertext[^1] ^= 0xFF;

        Assert.Throws<VaultCorruptedException>(() => _crypto.DecryptPayload(material.Key, material.Header, payload));
    }

    [Fact]
    public void Payload_cannot_be_moved_to_another_vault_header()
    {
        VaultKeyMaterial first = _crypto.CreateKeyMaterial(Password);
        VaultKeyMaterial second = _crypto.CreateKeyMaterial(Password);
        SealedData payload = _crypto.EncryptPayload(first.Key, first.Header, "data"u8);

        Assert.Throws<VaultCorruptedException>(() => _crypto.DecryptPayload(first.Key, second.Header, payload));
    }

    [Fact]
    public void Each_vault_gets_unique_salt_and_data_key()
    {
        VaultKeyMaterial first = _crypto.CreateKeyMaterial(Password);
        VaultKeyMaterial second = _crypto.CreateKeyMaterial(Password);

        Assert.NotEqual(first.Header.Kdf.Salt.ToArray(), second.Header.Kdf.Salt.ToArray());
        Assert.NotEqual(first.Header.WrappedKey.Ciphertext, second.Header.WrappedKey.Ciphertext);
    }

    [Fact]
    public void Weaker_kdf_parameters_require_upgrade()
    {
        VaultKeyMaterial weak = _crypto.CreateKeyMaterial(Password);
        VaultCryptoService stronger = TestFactory.CreateCryptoService(new VaultSecurityOptions { MemoryKiB = 2048, Iterations = 2, Parallelism = 1 });

        Assert.False(_crypto.RequiresUpgrade(weak.Header));
        Assert.True(stronger.RequiresUpgrade(weak.Header));
    }

    [Fact]
    public void Default_policy_uses_owasp_grade_argon2id()
    {
        VaultSecurityOptions options = VaultSecurityOptions.Default;

        Assert.Equal(KdfAlgorithm.Argon2id, options.Kdf);
        Assert.Equal(CipherAlgorithm.Aes256Gcm, options.Cipher);
        Assert.True(options.MemoryKiB >= 19 * 1024);
        Assert.True(options.Iterations >= 2);
        Assert.True(options.SaltSize >= 16);
    }
}
