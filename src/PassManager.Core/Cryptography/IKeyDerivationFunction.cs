namespace PassManager.Core.Cryptography;

/// <summary>Password-based key derivation (memory-hard, deliberately slow).</summary>
public interface IKeyDerivationFunction
{
    KdfAlgorithm Algorithm { get; }

    void DeriveKey(ReadOnlySpan<byte> password, KdfParameters parameters, Span<byte> destination);
}
