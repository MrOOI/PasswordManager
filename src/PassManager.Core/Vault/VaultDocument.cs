namespace PassManager.Core.Vault;

/// <summary>The decrypted content of a vault.</summary>
public sealed record VaultDocument(int SchemaVersion, IReadOnlyList<VaultEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
}
