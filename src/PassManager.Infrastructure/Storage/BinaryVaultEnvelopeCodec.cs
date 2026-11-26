using System.Buffers.Binary;
using PassManager.Core.Cryptography;
using PassManager.Core.Vault;

namespace PassManager.Infrastructure.Storage;

/// <summary>
/// Vault container format, version 1 (all integers little-endian):
/// <code>
/// magic        8   "PMVAULT\0"
/// version      u16
/// cipher       u8      kdf         u8
/// kdfMemoryKiB i32     kdfIterations i32    kdfParallelism i32
/// salt         u8 length + bytes
/// wrappedKey   nonce (u8 len + bytes) | tag (u8 len + bytes) | ciphertext (i32 len + bytes)
/// payload      nonce (u8 len + bytes) | tag (u8 len + bytes) | ciphertext (i32 len + bytes)
/// </code>
/// Every field is bounds-checked on read so a malicious file cannot trigger huge allocations or
/// absurd KDF costs (denial of service). Integrity of the content itself is enforced by AES-GCM.
/// </summary>
public sealed class BinaryVaultEnvelopeCodec : IVaultEnvelopeCodec
{
    public const ushort SupportedVersion = 1;

    internal const int MinSaltLength = 16;
    internal const int MinKdfMemoryKiB = 8;
    internal const int MaxKdfMemoryKiB = 4 * 1024 * 1024; // 4 GiB
    internal const int MaxKdfIterations = 64;
    internal const int MaxKdfParallelism = 64;
    internal const int MaxWrappedKeyLength = 1024;
    internal const int MaxPayloadLength = 256 * 1024 * 1024;

    private static ReadOnlySpan<byte> Magic => "PMVAULT\0"u8;

    public byte[] Encode(VaultEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        VaultHeader header = envelope.Header;

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(checked((ushort)header.FormatVersion));
            writer.Write((byte)header.Cipher);
            writer.Write((byte)header.Kdf.Algorithm);
            writer.Write(header.Kdf.MemoryKiB);
            writer.Write(header.Kdf.Iterations);
            writer.Write(header.Kdf.Parallelism);
            WriteShortBlock(writer, header.Kdf.Salt.Span);
            WriteSealed(writer, header.WrappedKey);
            WriteSealed(writer, envelope.Payload);
        }

        return stream.ToArray();
    }

    public VaultEnvelope Decode(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);

        if (data.Length < Magic.Length || !reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
        {
            throw new VaultCorruptedException("The file is not a PassManager vault.");
        }

        ushort version = reader.ReadUInt16();
        if (version == 0)
        {
            throw new VaultCorruptedException("The vault header is invalid.");
        }

        if (version > SupportedVersion)
        {
            throw new UnsupportedVaultFormatException(
                $"This vault was created by a newer version of PassManager (format {version}). Please update the application.");
        }

        var cipher = (CipherAlgorithm)reader.ReadByte();
        if (!Enum.IsDefined(cipher))
        {
            throw new UnsupportedVaultFormatException($"Unknown cipher identifier {(byte)cipher}.");
        }

        var kdfAlgorithm = (KdfAlgorithm)reader.ReadByte();
        if (!Enum.IsDefined(kdfAlgorithm))
        {
            throw new UnsupportedVaultFormatException($"Unknown key derivation identifier {(byte)kdfAlgorithm}.");
        }

        int memoryKiB = reader.ReadInt32();
        int iterations = reader.ReadInt32();
        int parallelism = reader.ReadInt32();
        ValidateKdfCost(memoryKiB, iterations, parallelism);

        byte[] salt = reader.ReadShortBlock();
        if (salt.Length < MinSaltLength)
        {
            throw new VaultCorruptedException("The vault salt is too short.");
        }

        SealedData wrappedKey = ReadSealed(ref reader, MaxWrappedKeyLength);
        SealedData payload = ReadSealed(ref reader, MaxPayloadLength);

        if (!reader.IsAtEnd)
        {
            throw new VaultCorruptedException("The vault file contains unexpected trailing data.");
        }

        var kdf = new KdfParameters(kdfAlgorithm, salt, memoryKiB, iterations, parallelism);
        return new VaultEnvelope(new VaultHeader(version, cipher, kdf, wrappedKey), payload);
    }

    private static void ValidateKdfCost(int memoryKiB, int iterations, int parallelism)
    {
        bool valid = parallelism is >= 1 and <= MaxKdfParallelism
            && iterations is >= 1 and <= MaxKdfIterations
            && memoryKiB >= Math.Max(MinKdfMemoryKiB, 8 * parallelism)
            && memoryKiB <= MaxKdfMemoryKiB;

        if (!valid)
        {
            throw new VaultCorruptedException("The vault key derivation parameters are out of the allowed range.");
        }
    }

    private static void WriteSealed(BinaryWriter writer, SealedData sealedData)
    {
        WriteShortBlock(writer, sealedData.Nonce);
        WriteShortBlock(writer, sealedData.Tag);
        writer.Write(sealedData.Ciphertext.Length);
        writer.Write(sealedData.Ciphertext);
    }

    private static void WriteShortBlock(BinaryWriter writer, ReadOnlySpan<byte> value)
    {
        if (value.Length > byte.MaxValue)
        {
            throw new ArgumentException("Block is too long for a one-byte length prefix.", nameof(value));
        }

        writer.Write((byte)value.Length);
        writer.Write(value);
    }

    private static SealedData ReadSealed(ref SpanReader reader, int maxCiphertextLength)
    {
        byte[] nonce = reader.ReadShortBlock();
        byte[] tag = reader.ReadShortBlock();

        int length = reader.ReadInt32();
        if (length < 0 || length > maxCiphertextLength)
        {
            throw new VaultCorruptedException("The vault contains a block with an invalid length.");
        }

        byte[] ciphertext = reader.ReadBytes(length).ToArray();
        return new SealedData(nonce, ciphertext, tag);
    }

    /// <summary>Forward-only reader that turns any overrun into a <see cref="VaultCorruptedException"/>.</summary>
    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public SpanReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public readonly bool IsAtEnd => _position == _data.Length;

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            if (count < 0 || count > _data.Length - _position)
            {
                throw new VaultCorruptedException("The vault file is truncated or damaged.");
            }

            ReadOnlySpan<byte> slice = _data.Slice(_position, count);
            _position += count;
            return slice;
        }

        public byte ReadByte() => ReadBytes(1)[0];

        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)));

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));

        public byte[] ReadShortBlock() => ReadBytes(ReadByte()).ToArray();
    }
}
