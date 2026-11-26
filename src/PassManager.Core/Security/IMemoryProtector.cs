namespace PassManager.Core.Security;

/// <summary>
/// Encrypts secret material in place while it sits idle in process memory, so that memory dumps,
/// crash dumps or the page file do not contain the raw key.
/// </summary>
/// <remarks>Buffers passed to this abstraction must be pinned and a multiple of <see cref="BlockSize"/>.</remarks>
public interface IMemoryProtector
{
    int BlockSize { get; }

    void Protect(Span<byte> buffer);

    void Unprotect(Span<byte> buffer);
}
