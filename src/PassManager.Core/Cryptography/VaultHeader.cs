namespace PassManager.Core.Cryptography;

/// <summary>
/// Public (unencrypted but authenticated) part of a vault: how to derive the key-encryption key and
/// the data key wrapped by it.
/// </summary>
public sealed record VaultHeader(int FormatVersion, CipherAlgorithm Cipher, KdfParameters Kdf, SealedData WrappedKey)
{
    public const int CurrentFormatVersion = 1;
}

/// <summary>A complete vault as stored on disk: header plus encrypted payload.</summary>
public sealed record VaultEnvelope(VaultHeader Header, SealedData Payload);
