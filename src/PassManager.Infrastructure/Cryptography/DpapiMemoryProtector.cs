using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using PassManager.Core.Security;

namespace PassManager.Infrastructure.Cryptography;

/// <summary>
/// Windows DPAPI in-memory encryption (CryptProtectMemory). The key used is held by the OS and is only
/// valid inside this process, so a dump of the process memory alone does not reveal the vault key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class DpapiMemoryProtector : IMemoryProtector
{
    private const int CryptProtectMemoryBlockSize = 16;
    private const uint CryptProtectMemorySameProcess = 0x00;

    public int BlockSize => CryptProtectMemoryBlockSize;

    public void Protect(Span<byte> buffer)
    {
        Validate(buffer);
        if (!CryptProtectMemory(ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length, CryptProtectMemorySameProcess))
        {
            throw new CryptographicException("CryptProtectMemory failed.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    public void Unprotect(Span<byte> buffer)
    {
        Validate(buffer);
        if (!CryptUnprotectMemory(ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length, CryptProtectMemorySameProcess))
        {
            throw new CryptographicException("CryptUnprotectMemory failed.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    private static void Validate(Span<byte> buffer)
    {
        if (buffer.IsEmpty || buffer.Length % CryptProtectMemoryBlockSize != 0)
        {
            throw new ArgumentException($"Buffer length must be a non-zero multiple of {CryptProtectMemoryBlockSize}.", nameof(buffer));
        }
    }

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptProtectMemory(ref byte pDataIn, uint cbDataIn, uint dwFlags);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptUnprotectMemory(ref byte pDataIn, uint cbDataIn, uint dwFlags);
}
