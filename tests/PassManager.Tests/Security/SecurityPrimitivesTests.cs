using System.Security;
using PassManager.Core.Security;
using PassManager.Infrastructure.Cryptography;
using PassManager.Tests.TestSupport;

namespace PassManager.Tests.Security;

public sealed class SecurityPrimitivesTests
{
    [Fact]
    public void Dpapi_protector_round_trips_and_changes_bytes()
    {
        var protector = new DpapiMemoryProtector();
        byte[] original = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        byte[] buffer = GC.AllocateArray<byte>(32, pinned: true);
        original.CopyTo(buffer, 0);

        protector.Protect(buffer);
        Assert.NotEqual(original, buffer);

        protector.Unprotect(buffer);
        Assert.Equal(original, buffer);
    }

    [Fact]
    public void Protected_key_is_only_plaintext_inside_a_lease()
    {
        var protector = new XorMemoryProtector();
        byte[] key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        using var protectedKey = new ProtectedKey(key, protector);

        using (ProtectedKey.KeyLease lease = protectedKey.Acquire())
        {
            Assert.Equal(key, lease.Span.ToArray());
        }

        Assert.Equal(2, protector.ProtectCalls); // once on creation, once when the lease ended
    }

    [Fact]
    public void Disposed_protected_key_cannot_be_used()
    {
        var protectedKey = new ProtectedKey(new byte[32], new XorMemoryProtector());
        protectedKey.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            using ProtectedKey.KeyLease lease = protectedKey.Acquire();
        });
    }

    [Fact]
    public void Sensitive_bytes_cannot_be_read_after_dispose()
    {
        var bytes = SensitiveBytes.CopyFrom([1, 2, 3]);
        bytes.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bytes.Span.Length);
    }

    [Fact]
    public void Secure_string_is_encoded_as_utf8()
    {
        using SecureString secure = ToSecureString("pässwörd€");
        using SensitiveBytes bytes = secure.ToUtf8Bytes();

        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("pässwörd€"), bytes.ReadOnlySpan.ToArray());
    }

    [Theory]
    [InlineData("same-value", "same-value", true)]
    [InlineData("same-value", "same-valuE", false)]
    [InlineData("short", "longer value", false)]
    public void Secure_strings_are_compared_by_content(string left, string right, bool expected)
    {
        using SecureString a = ToSecureString(left);
        using SecureString b = ToSecureString(right);

        Assert.Equal(expected, a.ContentEquals(b));
    }

    private static SecureString ToSecureString(string value)
    {
        var secure = new SecureString();
        foreach (char c in value)
        {
            secure.AppendChar(c);
        }

        secure.MakeReadOnly();
        return secure;
    }
}
