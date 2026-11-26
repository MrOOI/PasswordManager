using System.Security.Cryptography;

namespace PassManager.Core.Security;

/// <summary>
/// Owns a buffer of secret material (derived keys, UTF-8 encoded passwords, decrypted payloads).
/// The buffer is allocated on the pinned object heap so the GC never relocates it (which would leave
/// stray copies in memory) and it is wiped deterministically when disposed.
/// </summary>
public sealed class SensitiveBytes : IDisposable
{
    private readonly byte[] _buffer;
    private bool _disposed;

    public SensitiveBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _buffer = GC.AllocateArray<byte>(length, pinned: true);
    }

    public int Length => _buffer.Length;

    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer;
        }
    }

    public ReadOnlySpan<byte> ReadOnlySpan => Span;

    public static SensitiveBytes CopyFrom(ReadOnlySpan<byte> source)
    {
        var result = new SensitiveBytes(source.Length);
        source.CopyTo(result._buffer);
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_buffer);
        _disposed = true;
    }
}
