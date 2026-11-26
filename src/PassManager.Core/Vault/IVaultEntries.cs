namespace PassManager.Core.Vault;

/// <summary>CRUD access to entries of an unlocked vault. Every change is persisted immediately.</summary>
public interface IVaultEntries
{
    IReadOnlyList<VaultEntry> GetEntries();

    Task<VaultEntry> AddEntryAsync(EntryDraft draft, CancellationToken cancellationToken = default);

    Task<VaultEntry> UpdateEntryAsync(Guid id, EntryDraft draft, CancellationToken cancellationToken = default);

    Task<VaultEntry> SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default);

    Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default);
}
