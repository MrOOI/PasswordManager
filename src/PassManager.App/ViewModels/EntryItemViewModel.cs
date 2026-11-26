using System.Globalization;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>Read-only presentation wrapper around an immutable <see cref="VaultEntry"/>.</summary>
public sealed class EntryItemViewModel
{
    public EntryItemViewModel(VaultEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public VaultEntry Entry { get; }

    public Guid Id => Entry.Id;

    public string Title => Entry.Title;

    public string Username => Entry.Username;

    public string Password => Entry.Password;

    public string Url => Entry.Url;

    public string Notes => Entry.Notes;

    public string Category => Entry.Category;

    public bool IsFavorite => Entry.IsFavorite;

    public bool HasUsername => Entry.Username.Length > 0;

    public bool HasPassword => Entry.Password.Length > 0;

    public bool HasUrl => Entry.Url.Length > 0;

    public bool HasNotes => Entry.Notes.Length > 0;

    public bool HasCategory => Entry.Category.Length > 0;

    public string Initial => Entry.Title.Length > 0 ? char.ToUpper(Entry.Title[0], CultureInfo.CurrentCulture).ToString() : "?";

    public string Subtitle =>
        HasUsername ? Entry.Username
        : WebAddress.TryNormalize(Entry.Url, out Uri? uri) ? uri.Host
        : HasCategory ? Entry.Category
        : "No username";

    public string Timestamps => string.Create(
        CultureInfo.CurrentCulture,
        $"Created {Entry.CreatedAt.ToLocalTime():g}  ·  Modified {Entry.ModifiedAt.ToLocalTime():g}");

    public bool Matches(string[] terms)
    {
        foreach (string term in terms)
        {
            bool found = Contains(Entry.Title, term)
                || Contains(Entry.Username, term)
                || Contains(Entry.Url, term)
                || Contains(Entry.Category, term)
                || Contains(Entry.Notes, term);

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string value, string term) => value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
}
