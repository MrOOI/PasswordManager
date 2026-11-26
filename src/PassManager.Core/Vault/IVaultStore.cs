namespace PassManager.Core.Vault;

/// <summary>Durable storage for the (already encrypted) vault bytes.</summary>
public interface IVaultStore
{
    string Location { get; }

    bool Exists();

    /// <exception cref="VaultNotFoundException">No vault exists.</exception>
    Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the stored vault.</summary>
    /// <param name="keepBackup">
    /// Keep the previous version as a backup. Pass <c>false</c> after re-keying so no copy encrypted
    /// under the old master password is left behind.
    /// </param>
    Task WriteAsync(ReadOnlyMemory<byte> content, bool keepBackup, CancellationToken cancellationToken = default);
}

/// <summary>Binary (de)serialisation of the on-disk container.</summary>
public interface IVaultEnvelopeCodec
{
    byte[] Encode(Cryptography.VaultEnvelope envelope);

    /// <exception cref="VaultCorruptedException">Malformed data.</exception>
    /// <exception cref="UnsupportedVaultFormatException">Unknown version or algorithm.</exception>
    Cryptography.VaultEnvelope Decode(ReadOnlySpan<byte> data);
}

/// <summary>(De)serialisation of the decrypted vault content.</summary>
public interface IVaultContentSerializer
{
    byte[] Serialize(VaultDocument document);

    VaultDocument Deserialize(ReadOnlySpan<byte> data);
}
