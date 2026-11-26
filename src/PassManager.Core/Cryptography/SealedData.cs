namespace PassManager.Core.Cryptography;

/// <summary>Output of an authenticated encryption operation.</summary>
public sealed record SealedData(byte[] Nonce, byte[] Ciphertext, byte[] Tag);
