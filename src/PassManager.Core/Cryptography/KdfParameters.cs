namespace PassManager.Core.Cryptography;

/// <summary>Everything needed to re-derive the key-encryption key from the master password.</summary>
public sealed class KdfParameters
{
    public KdfParameters(KdfAlgorithm algorithm, ReadOnlyMemory<byte> salt, int memoryKiB, int iterations, int parallelism)
    {
        if (salt.IsEmpty)
        {
            throw new ArgumentException("Salt must not be empty.", nameof(salt));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryKiB);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        Algorithm = algorithm;
        Salt = salt;
        MemoryKiB = memoryKiB;
        Iterations = iterations;
        Parallelism = parallelism;
    }

    public KdfAlgorithm Algorithm { get; }

    public ReadOnlyMemory<byte> Salt { get; }

    public int MemoryKiB { get; }

    public int Iterations { get; }

    public int Parallelism { get; }
}
