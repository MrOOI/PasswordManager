namespace PassManager.Core.Vault;

/// <summary>A single credential stored in the vault. Immutable: changes produce a new instance.</summary>
public sealed record VaultEntry
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public bool IsFavorite { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ModifiedAt { get; init; }

    internal static VaultEntry Create(EntryDraft draft, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Title = draft.Title,
        Username = draft.Username,
        Password = draft.Password,
        Url = draft.Url,
        Notes = draft.Notes,
        Category = draft.Category,
        IsFavorite = draft.IsFavorite,
        CreatedAt = now,
        ModifiedAt = now,
    };

    internal VaultEntry Apply(EntryDraft draft, DateTimeOffset now) => this with
    {
        Title = draft.Title,
        Username = draft.Username,
        Password = draft.Password,
        Url = draft.Url,
        Notes = draft.Notes,
        Category = draft.Category,
        IsFavorite = draft.IsFavorite,
        ModifiedAt = now,
    };

    /// <summary>Deliberately omits secrets so entries can never leak into logs via ToString().</summary>
    public override string ToString() => $"VaultEntry {{ Id = {Id}, Title = {Title} }}";
}
