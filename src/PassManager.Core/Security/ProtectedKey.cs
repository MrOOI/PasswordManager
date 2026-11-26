using System.Security.Cryptography;

namespace PassManager.Core.Security;

/// <summary>
/// Holds a symmetric key in a pinned buffer that stays encrypted (via <see cref="IMemoryProtector"/>)
/// except for the short moments it is actively used through a <see cref="KeyLease"/>.
/// </summary>
public sealed class ProtectedKey : IDisposable
{
    private readonly IMemoryProtector _protector;
    private readonly byte[] _buffer;
    private readonly int _length;
    private readonly Lock _sync = new();
    private bool _disposed;

    public ProtectedKey(ReadOnlySpan<byte> key, IMemoryProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        if (key.IsEmpty)
        {
            throw new ArgumentException("Key material must not be empty.", nameof(key));
        }

        _protector = protector;
        _length = key.Length;

        int blockSize = Math.Max(1, protector.BlockSize);
        int paddedLength = (key.Length + blockSize - 1) / blockSize * blockSize;
        _buffer = GC.AllocateArray<byte>(paddedLength, pinned: true);
        key.CopyTo(_buffer);
        _protector.Protect(_buffer);
    }

    public int Length => _length;

    /// <summary>
    /// Temporarily decrypts the key. Always dispose the lease (use <c>using</c>) so the key is re-protected.
    /// </summary>
    public KeyLease Acquire()
    {
        _sync.Enter();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _protector.Unprotect(_buffer);
            return new KeyLease(this);
        }
        catch
        {
            _sync.Exit();
            throw;
        }
    }

    private void Release()
    {
        try
        {
            _protector.Protect(_buffer);
        }
        catch
        {
            // Never leave a plaintext key behind if re-protection fails.
            CryptographicOperations.ZeroMemory(_buffer);
            _disposed = true;
            throw;
        }
        finally
        {
            _sync.Exit();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(_buffer);
            _disposed = true;
        }
    }

    /// <summary>Scoped access to the plaintext key. Cannot escape the stack or cross an <c>await</c>.</summary>
    public readonly ref struct KeyLease
    {
        private readonly ProtectedKey _owner;

        internal KeyLease(ProtectedKey owner) => _owner = owner;

        public ReadOnlySpan<byte> Span => _owner._buffer.AsSpan(0, _owner._length);

        public void Dispose() => _owner.Release();
    }
}
