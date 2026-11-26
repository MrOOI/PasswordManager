using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using PassManager.Core.Cryptography;

namespace PassManager.Infrastructure.Cryptography;

/// <summary>
/// Argon2id (RFC 9106), the memory-hard password hashing function recommended by OWASP.
/// Its memory cost makes GPU/ASIC brute-force attacks against a stolen vault file expensive.
/// </summary>
public sealed class Argon2idKeyDerivation : IKeyDerivationFunction
{
    public KdfAlgorithm Algorithm => KdfAlgorithm.Argon2id;

    public void DeriveKey(ReadOnlySpan<byte> password, KdfParameters parameters, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Algorithm != Algorithm)
        {
            throw new ArgumentException($"Expected {Algorithm} parameters but got {parameters.Algorithm}.", nameof(parameters));
        }

        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination must not be empty.", nameof(destination));
        }

        // The library requires an array: use a pinned copy that we can wipe deterministically.
        byte[] passwordCopy = GC.AllocateArray<byte>(password.Length, pinned: true);
        byte[]? derived = null;
        try
        {
            password.CopyTo(passwordCopy);

            using var argon2 = new Argon2id(passwordCopy)
            {
                Salt = parameters.Salt.ToArray(),
                MemorySize = parameters.MemoryKiB,
                Iterations = parameters.Iterations,
                DegreeOfParallelism = parameters.Parallelism,
            };

            derived = argon2.GetBytes(destination.Length);
            derived.CopyTo(destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordCopy);
            if (derived is not null)
            {
                CryptographicOperations.ZeroMemory(derived);
            }
        }
    }
}
