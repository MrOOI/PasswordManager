namespace PassManager.App.ViewModels;

public enum EntryFilterKind
{
    All,
    Favorites,
    Category,
}

/// <summary>A sidebar item that narrows the entry list.</summary>
public sealed class EntryFilterViewModel
{
    private EntryFilterViewModel(EntryFilterKind kind, string name, string glyph, int count)
    {
        Kind = kind;
        Name = name;
        Glyph = glyph;
        Count = count;
    }

    public EntryFilterKind Kind { get; }

    public string Name { get; }

    public string Glyph { get; }

    public int Count { get; }

    /// <summary>Stable identity used to restore the selection after the list is rebuilt.</summary>
    public string Key => Kind == EntryFilterKind.Category ? "category:" + Name.ToUpperInvariant() : Kind.ToString();

    public static EntryFilterViewModel All(int count) => new(EntryFilterKind.All, "All items", "", count);

    public static EntryFilterViewModel Favorites(int count) => new(EntryFilterKind.Favorites, "Favorites", "", count);

    public static EntryFilterViewModel ForCategory(string name, int count) => new(EntryFilterKind.Category, name, "", count);

    public bool Includes(EntryItemViewModel item) => Kind switch
    {
        EntryFilterKind.Favorites => item.IsFavorite,
        EntryFilterKind.Category => string.Equals(item.Category, Name, StringComparison.CurrentCultureIgnoreCase),
        _ => true,
    };
}
