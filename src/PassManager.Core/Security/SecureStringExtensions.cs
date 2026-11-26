using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace PassManager.Core.Security;

public delegate TResult CharSpanFunc<out TResult>(ReadOnlySpan<char> chars);

/// <summary>
/// Helpers that read a <see cref="SecureString"/> (as produced by WPF's PasswordBox) without ever
/// materialising it as an immutable, un-wipeable managed <see cref="string"/>.
/// </summary>
public static class SecureStringExtensions
{
    /// <summary>
    /// Exposes the characters of <paramref name="value"/> to <paramref name="func"/> through a span over
    /// unmanaged memory that is zeroed and freed immediately afterwards.
    /// </summary>
    public static unsafe TResult UseChars<TResult>(this SecureString value, CharSpanFunc<TResult> func)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(func);

        IntPtr pointer = Marshal.SecureStringToGlobalAllocUnicode(value);
        try
        {
            return func(new ReadOnlySpan<char>((void*)pointer, value.Length));
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
        }
    }

    /// <summary>Encodes the secure string as UTF-8 into a pinned, self-wiping buffer.</summary>
    public static SensitiveBytes ToUtf8Bytes(this SecureString value) =>
        value.UseChars(static chars =>
        {
            var bytes = new SensitiveBytes(Encoding.UTF8.GetByteCount(chars));
            Encoding.UTF8.GetBytes(chars, bytes.Span);
            return bytes;
        });

    /// <summary>Compares two secure strings without creating managed string copies.</summary>
    public static unsafe bool ContentEquals(this SecureString value, SecureString other)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(other);

        if (value.Length != other.Length)
        {
            return false;
        }

        IntPtr left = Marshal.SecureStringToGlobalAllocUnicode(value);
        IntPtr right = Marshal.SecureStringToGlobalAllocUnicode(other);
        try
        {
            var leftBytes = new ReadOnlySpan<byte>((void*)left, value.Length * sizeof(char));
            var rightBytes = new ReadOnlySpan<byte>((void*)right, other.Length * sizeof(char));
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(left);
            Marshal.ZeroFreeGlobalAllocUnicode(right);
        }
    }
}
