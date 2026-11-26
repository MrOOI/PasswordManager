using System.Security.Cryptography;
using PassManager.Core.Security;
using PassManager.Core.Vault;

namespace PassManager.Core.Cryptography;

/// <inheritdoc />
/// <remarks>
/// Algorithms are resolved by the identifier stored in the header, so new KDFs or ciphers can be
/// added by registering another implementation (Open/Closed principle) without touching this class.
/// Every ciphertext is bound to its context through associated data with a distinct domain-separation label,
/// so header fields (KDF cost, salt, algorithms) cannot be altered without detection.
/// </remarks>
public sealed class VaultCryptoService : IVaultCryptoService
{
    private static readonly byte[] KeyWrapLabel = "PassManager/v1/key-wrap"u8.ToArray();
    private static readonly byte[] PayloadLabel = "PassManager/v1/payload"u8.ToArray();

    private readonly Dictionary<KdfAlgorithm, IKeyDerivationFunction> _kdfs;
    private readonly Dictionary<CipherAlgorithm, IAuthenticatedCipher> _ciphers;
    private readonly IMemoryProtector _memoryProtector;
    private readonly VaultSecurityOptions _options;

    public VaultCryptoService(
        IEnumerable<IKeyDerivationFunction> keyDerivationFunctions,
        IEnumerable<IAuthenticatedCipher> ciphers,
        IMemoryProtector memoryProtector,
        VaultSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(keyDerivationFunctions);
        ArgumentNullException.ThrowIfNull(ciphers);
        ArgumentNullException.ThrowIfNull(memoryProtector);
        ArgumentNullException.ThrowIfNull(options);

        _kdfs = keyDerivationFunctions.ToDictionary(k => k.Algorithm);
        _ciphers = ciphers.ToDictionary(c => c.Algorithm);
        _memoryProtector = memoryProtector;
        _options = options;
    }

    public VaultKeyMaterial CreateKeyMaterial(ReadOnlySpan<byte> masterPassword)
    {
        IAuthenticatedCipher cipher = GetCipher(_options.Cipher);
        var kdf = new KdfParameters(
            _options.Kdf,
            RandomNumberGenerator.GetBytes(_options.SaltSize),
            _options.MemoryKiB,
            _options.Iterations,
            _options.Parallelism);

        using var dataKey = new SensitiveBytes(cipher.KeySize);
        RandomNumberGenerator.Fill(dataKey.Span);

        using SensitiveBytes keyEncryptionKey = DeriveKeyEncryptionKey(masterPassword, kdf, cipher.KeySize);
        byte[] associatedData = BuildKeyWrapAssociatedData(VaultHeader.CurrentFormatVersion, cipher.Algorithm, kdf);
        SealedData wrappedKey = cipher.Encrypt(keyEncryptionKey.ReadOnlySpan, dataKey.ReadOnlySpan, associatedData);

        var header = new VaultHeader(VaultHeader.CurrentFormatVersion, cipher.Algorithm, kdf, wrappedKey);
        return new VaultKeyMaterial(new ProtectedKey(dataKey.ReadOnlySpan, _memoryProtector), header);
    }

    public ProtectedKey UnlockKey(ReadOnlySpan<byte> masterPassword, VaultHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        IAuthenticatedCipher cipher = GetCipher(header.Cipher);
        if (header.WrappedKey.Ciphertext.Length != cipher.KeySize)
        {
            throw new VaultCorruptedException("The vault header contains a key of an unexpected size.");
        }

        using SensitiveBytes keyEncryptionKey = DeriveKeyEncryptionKey(masterPassword, header.Kdf, cipher.KeySize);
        using var dataKey = new SensitiveBytes(cipher.KeySize);
        byte[] associatedData = BuildKeyWrapAssociatedData(header.FormatVersion, header.Cipher, header.Kdf);

        try
        {
            cipher.Decrypt(keyEncryptionKey.ReadOnlySpan, header.WrappedKey, associatedData, dataKey.Span);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidMasterPasswordException(ex);
        }

        return new ProtectedKey(dataKey.ReadOnlySpan, _memoryProtector);
    }

    public bool RequiresUpgrade(VaultHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        return header.FormatVersion < VaultHeader.CurrentFormatVersion
            || header.Cipher != _options.Cipher
            || header.Kdf.Algorithm != _options.Kdf
            || header.Kdf.MemoryKiB < _options.MemoryKiB
            || header.Kdf.Iterations < _options.Iterations
            || header.Kdf.Salt.Length < _options.SaltSize;
    }

    public SealedData EncryptPayload(ProtectedKey key, VaultHeader header, ReadOnlySpan<byte> plaintext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(header);

        IAuthenticatedCipher cipher = GetCipher(header.Cipher);
        byte[] associatedData = BuildPayloadAssociatedData(header);

        using ProtectedKey.KeyLease lease = key.Acquire();
        return cipher.Encrypt(lease.Span, plaintext, associatedData);
    }

    public SensitiveBytes DecryptPayload(ProtectedKey key, VaultHeader header, SealedData payload)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(payload);

        IAuthenticatedCipher cipher = GetCipher(header.Cipher);
        byte[] associatedData = BuildPayloadAssociatedData(header);
        var plaintext = new SensitiveBytes(payload.Ciphertext.Length);

        try
        {
            using ProtectedKey.KeyLease lease = key.Acquire();
            cipher.Decrypt(lease.Span, payload, associatedData, plaintext.Span);
            return plaintext;
        }
        catch (CryptographicException ex)
        {
            plaintext.Dispose();
            throw new VaultCorruptedException("The vault contents failed integrity verification. The file may be damaged or tampered with.", ex);
        }
    }

    private SensitiveBytes DeriveKeyEncryptionKey(ReadOnlySpan<byte> masterPassword, KdfParameters parameters, int keySize)
    {
        if (!_kdfs.TryGetValue(parameters.Algorithm, out IKeyDerivationFunction? kdf))
        {
            throw new UnsupportedVaultFormatException($"Key derivation algorithm '{parameters.Algorithm}' is not supported.");
        }

        var key = new SensitiveBytes(keySize);
        try
        {
            kdf.DeriveKey(masterPassword, parameters, key.Span);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    private IAuthenticatedCipher GetCipher(CipherAlgorithm algorithm) =>
        _ciphers.TryGetValue(algorithm, out IAuthenticatedCipher? cipher)
            ? cipher
            : throw new UnsupportedVaultFormatException($"Cipher '{algorithm}' is not supported.");

    private static byte[] BuildKeyWrapAssociatedData(int formatVersion, CipherAlgorithm cipher, KdfParameters kdf)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            WritePrefixed(writer, KeyWrapLabel);
            WriteCommonHeaderFields(writer, formatVersion, cipher, kdf);
        }

        return stream.ToArray();
    }

    private static byte[] BuildPayloadAssociatedData(VaultHeader header)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            WritePrefixed(writer, PayloadLabel);
            WriteCommonHeaderFields(writer, header.FormatVersion, header.Cipher, header.Kdf);
            WritePrefixed(writer, header.WrappedKey.Nonce);
            WritePrefixed(writer, header.WrappedKey.Ciphertext);
            WritePrefixed(writer, header.WrappedKey.Tag);
        }

        return stream.ToArray();
    }

    private static void WriteCommonHeaderFields(BinaryWriter writer, int formatVersion, CipherAlgorithm cipher, KdfParameters kdf)
    {
        writer.Write(formatVersion);
        writer.Write((byte)cipher);
        writer.Write((byte)kdf.Algorithm);
        writer.Write(kdf.MemoryKiB);
        writer.Write(kdf.Iterations);
        writer.Write(kdf.Parallelism);
        WritePrefixed(writer, kdf.Salt.Span);
    }

    private static void WritePrefixed(BinaryWriter writer, ReadOnlySpan<byte> value)
    {
        writer.Write(value.Length);
        writer.Write(value);
    }
}
