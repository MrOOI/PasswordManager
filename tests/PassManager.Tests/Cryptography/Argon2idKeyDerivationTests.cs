using System.Text;
using PassManager.Core.Cryptography;
using PassManager.Infrastructure.Cryptography;

namespace PassManager.Tests.Cryptography;

public sealed class Argon2idKeyDerivationTests
{
    private readonly Argon2idKeyDerivation _kdf = new();

    [Fact]
    public void Matches_reference_implementation_test_vector()
    {
        // Vector from the Argon2 reference implementation (phc-winner-argon2, src/test.c):
        // argon2id v=19, m=65536 KiB, t=2, p=1, password "password", salt "somesalt".
        var parameters = new KdfParameters(KdfAlgorithm.Argon2id, Encoding.ASCII.GetBytes("somesalt"), 65536, 2, 1);
        byte[] output = new byte[32];

        _kdf.DeriveKey(Encoding.ASCII.GetBytes("password"), parameters, output);

        Assert.Equal("09316115d5cf24ed5a15a31a3ba326e5cf32edc24702987c02b6566f61913cf7", Convert.ToHexStringLower(output));
    }

    [Fact]
    public void Same_inputs_produce_same_key()
    {
        KdfParameters parameters = Parameters("0123456789abcdef"u8.ToArray());

        Assert.Equal(Derive("correct horse", parameters), Derive("correct horse", parameters));
    }

    [Fact]
    public void Different_salt_produces_different_key()
    {
        byte[] first = Derive("correct horse", Parameters("0123456789abcdef"u8.ToArray()));
        byte[] second = Derive("correct horse", Parameters("fedcba9876543210"u8.ToArray()));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Different_password_produces_different_key()
    {
        KdfParameters parameters = Parameters("0123456789abcdef"u8.ToArray());

        Assert.NotEqual(Derive("correct horse", parameters), Derive("correct hors3", parameters));
    }

    private static KdfParameters Parameters(byte[] salt) => new(KdfAlgorithm.Argon2id, salt, 1024, 1, 1);

    private byte[] Derive(string password, KdfParameters parameters)
    {
        byte[] output = new byte[32];
        _kdf.DeriveKey(Encoding.UTF8.GetBytes(password), parameters, output);
        return output;
    }
}
