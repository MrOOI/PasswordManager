using Microsoft.Extensions.Logging.Abstractions;
using PassManager.Core.Cryptography;
using PassManager.Core.Security;
using PassManager.Core.Vault;
using PassManager.Infrastructure.Cryptography;
using PassManager.Infrastructure.Storage;

namespace PassManager.Tests.TestSupport;

/// <summary>In-memory <see cref="IVaultStore"/> for fast, isolated tests.</summary>
internal sealed class InMemoryVaultStore : IVaultStore
{
    public byte[]? Content { get; private set; }

    public int WriteCount { get; private set; }

    public bool FailWrites { get; set; }

    public string Location => "memory://vault";

    public bool Exists() => Content is not null;

    public Task<byte[]> ReadAsync(CancellationToken cancellationToken = default) =>
        Content is null ? throw new VaultNotFoundException(Location) : Task.FromResult(Content.ToArray());

    public Task WriteAsync(ReadOnlyMemory<byte> content, bool keepBackup, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("Simulated disk failure.");
        }

        Content = content.ToArray();
        WriteCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Reversible XOR "protection" that makes it observable whether data is protected.</summary>
internal sealed class XorMemoryProtector : IMemoryProtector
{
    public int BlockSize => 16;

    public int ProtectCalls { get; private set; }

    public void Protect(Span<byte> buffer)
    {
        ProtectCalls++;
        Xor(buffer);
    }

    public void Unprotect(Span<byte> buffer) => Xor(buffer);

    private static void Xor(Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] ^= 0x5A;
        }
    }
}

internal static class TestFactory
{
    /// <summary>Cheap KDF parameters so tests run quickly. Never use outside tests.</summary>
    public static VaultSecurityOptions FastSecurityOptions { get; } = new()
    {
        MemoryKiB = 1024,
        Iterations = 1,
        Parallelism = 1,
    };

    public static VaultCryptoService CreateCryptoService(VaultSecurityOptions? options = null) =>
        new([new Argon2idKeyDerivation()], [new AesGcmCipher()], new DpapiMemoryProtector(), options ?? FastSecurityOptions);

    public static VaultSession CreateSession(IVaultStore store, VaultSecurityOptions? options = null) =>
        new(
            store,
            new BinaryVaultEnvelopeCodec(),
            new JsonVaultContentSerializer(),
            CreateCryptoService(options),
            TimeProvider.System,
            NullLogger<VaultSession>.Instance);

    public static SensitiveBytes Password(string value) => SensitiveBytes.CopyFrom(System.Text.Encoding.UTF8.GetBytes(value));

    public static EntryDraft Draft(string title, string password = "S3cret!Pass") => new()
    {
        Title = title,
        Username = $"{title.ToLowerInvariant()}@example.com",
        Password = password,
        Url = "https://example.com",
        Category = "Work",
    };
}
