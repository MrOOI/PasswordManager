using System.Buffers.Binary;
using PassManager.Core.Cryptography;
using PassManager.Core.Vault;
using PassManager.Infrastructure.Storage;

namespace PassManager.Tests.Storage;

public sealed class BinaryVaultEnvelopeCodecTests
{
    // Offset of the KDF memory field: magic(8) + version(2) + cipher(1) + kdf(1).
    private const int MemoryFieldOffset = 12;

    private readonly BinaryVaultEnvelopeCodec _codec = new();

    [Fact]
    public void Encode_then_decode_round_trips_every_field()
    {
        VaultEnvelope original = CreateEnvelope();

        VaultEnvelope decoded = _codec.Decode(_codec.Encode(original));

        Assert.Equal(original.Header.FormatVersion, decoded.Header.FormatVersion);
        Assert.Equal(original.Header.Cipher, decoded.Header.Cipher);
        Assert.Equal(original.Header.Kdf.Algorithm, decoded.Header.Kdf.Algorithm);
        Assert.Equal(original.Header.Kdf.MemoryKiB, decoded.Header.Kdf.MemoryKiB);
        Assert.Equal(original.Header.Kdf.Iterations, decoded.Header.Kdf.Iterations);
        Assert.Equal(original.Header.Kdf.Parallelism, decoded.Header.Kdf.Parallelism);
        Assert.Equal(original.Header.Kdf.Salt.ToArray(), decoded.Header.Kdf.Salt.ToArray());
        AssertSealedEqual(original.Header.WrappedKey, decoded.Header.WrappedKey);
        AssertSealedEqual(original.Payload, decoded.Payload);
    }

    [Fact]
    public void Rejects_files_without_the_magic_marker()
    {
        byte[] data = _codec.Encode(CreateEnvelope());
        data[0] = (byte)'X';

        Assert.Throws<VaultCorruptedException>(() => _codec.Decode(data));
    }

    [Fact]
    public void Rejects_every_truncation()
    {
        byte[] data = _codec.Encode(CreateEnvelope());

        for (int length = 0; length < data.Length; length++)
        {
            byte[] truncated = data[..length];
            Assert.Throws<VaultCorruptedException>(() => _codec.Decode(truncated));
        }
    }

    [Fact]
    public void Rejects_trailing_data()
    {
        byte[] data = [.. _codec.Encode(CreateEnvelope()), 0x00];

        Assert.Throws<VaultCorruptedException>(() => _codec.Decode(data));
    }

    [Fact]
    public void Rejects_vaults_from_a_newer_format_version()
    {
        byte[] data = _codec.Encode(CreateEnvelope());
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), BinaryVaultEnvelopeCodec.SupportedVersion + 1);

        Assert.Throws<UnsupportedVaultFormatException>(() => _codec.Decode(data));
    }

    [Fact]
    public void Rejects_absurd_kdf_memory_cost_to_prevent_denial_of_service()
    {
        byte[] data = _codec.Encode(CreateEnvelope());
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(MemoryFieldOffset), int.MaxValue);

        Assert.Throws<VaultCorruptedException>(() => _codec.Decode(data));
    }

    private static VaultEnvelope CreateEnvelope()
    {
        var kdf = new KdfParameters(KdfAlgorithm.Argon2id, Enumerable.Repeat((byte)7, 32).ToArray(), 65536, 3, 4);
        var wrappedKey = new SealedData(Fill(12, 1), Fill(32, 2), Fill(16, 3));
        var header = new VaultHeader(VaultHeader.CurrentFormatVersion, CipherAlgorithm.Aes256Gcm, kdf, wrappedKey);
        var payload = new SealedData(Fill(12, 4), Fill(300, 5), Fill(16, 6));
        return new VaultEnvelope(header, payload);
    }

    private static byte[] Fill(int length, byte value) => Enumerable.Repeat(value, length).ToArray();

    private static void AssertSealedEqual(SealedData expected, SealedData actual)
    {
        Assert.Equal(expected.Nonce, actual.Nonce);
        Assert.Equal(expected.Ciphertext, actual.Ciphertext);
        Assert.Equal(expected.Tag, actual.Tag);
    }
}
