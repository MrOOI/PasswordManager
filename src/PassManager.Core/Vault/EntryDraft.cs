namespace PassManager.Core.Vault;

public static class EntryLimits
{
    public const int TitleMaxLength = 256;
    public const int UsernameMaxLength = 512;
    public const int PasswordMaxLength = 1024;
    public const int UrlMaxLength = 2048;
    public const int CategoryMaxLength = 64;
    public const int NotesMaxLength = 20_000;
}

/// <summary>User-supplied data for creating or updating an entry.</summary>
public sealed record EntryDraft
{
    public string Title { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public bool IsFavorite { get; init; }

    /// <summary>Trims identifying fields. The password is intentionally kept byte-for-byte.</summary>
    public EntryDraft Normalize() => this with
    {
        Title = (Title ?? string.Empty).Trim(),
        Username = (Username ?? string.Empty).Trim(),
        Password = Password ?? string.Empty,
        Url = (Url ?? string.Empty).Trim(),
        Notes = Notes ?? string.Empty,
        Category = (Category ?? string.Empty).Trim(),
    };

    public IReadOnlyList<string> GetValidationErrors()
    {
        EntryDraft draft = Normalize();
        var errors = new List<string>();

        if (draft.Title.Length == 0)
        {
            errors.Add("Title is required.");
        }

        AddLengthError(errors, "Title", draft.Title, EntryLimits.TitleMaxLength);
        AddLengthError(errors, "Username", draft.Username, EntryLimits.UsernameMaxLength);
        AddLengthError(errors, "Password", draft.Password, EntryLimits.PasswordMaxLength);
        AddLengthError(errors, "Website", draft.Url, EntryLimits.UrlMaxLength);
        AddLengthError(errors, "Category", draft.Category, EntryLimits.CategoryMaxLength);
        AddLengthError(errors, "Notes", draft.Notes, EntryLimits.NotesMaxLength);

        if (draft.Url.Length > 0 && !WebAddress.TryNormalize(draft.Url, out _))
        {
            errors.Add("Website must be a valid http:// or https:// address.");
        }

        return errors;
    }

    public override string ToString() => $"EntryDraft {{ Title = {Title} }}";

    private static void AddLengthError(List<string> errors, string field, string value, int maxLength)
    {
        if (value.Length > maxLength)
        {
            errors.Add($"{field} must be at most {maxLength} characters.");
        }
    }
}
