using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PassManager.App.Services;
using PassManager.Core.Passwords;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>Main screen of an unlocked vault: filters, searchable entry list and entry details.</summary>
public sealed partial class VaultViewModel : ObservableObject, INavigationAware
{
    private const string MaskedPassword = "••••••••••••";

    private readonly IVaultEntries _entries;
    private readonly IVaultLifecycle _lifecycle;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IViewModelFactory _viewModels;
    private readonly IShellLauncher _shell;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;
    private readonly ILogger<VaultViewModel> _logger;
    private readonly BulkObservableCollection<EntryItemViewModel> _items = [];
    private readonly ListCollectionView _entriesView;
    private CancellationTokenSource? _statusCancellation;
    private string[] _searchTerms = [];

    [ObservableProperty]
    private EntryFilterViewModel? _selectedFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPasswordDisplay))]
    [NotifyCanExecuteChangedFor(
        nameof(EditEntryCommand),
        nameof(DeleteEntryCommand),
        nameof(ToggleFavoriteCommand),
        nameof(CopyUsernameCommand),
        nameof(CopyPasswordCommand),
        nameof(CopyUrlCommand),
        nameof(OpenUrlCommand))]
    private EntryItemViewModel? _selectedEntry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPasswordDisplay))]
    private bool _isPasswordRevealed;

    [ObservableProperty]
    private PasswordStrengthResult? _selectedPasswordStrength;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListEmpty), nameof(EmptyListMessage), nameof(VisibleCountText))]
    private int _visibleCount;

    public VaultViewModel(
        IVaultEntries entries,
        IVaultLifecycle lifecycle,
        IClipboardService clipboard,
        IDialogService dialogs,
        IViewModelFactory viewModels,
        IShellLauncher shell,
        IPasswordStrengthEvaluator strengthEvaluator,
        ILogger<VaultViewModel> logger)
    {
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _strengthEvaluator = strengthEvaluator ?? throw new ArgumentNullException(nameof(strengthEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _entriesView = new ListCollectionView(_items)
        {
            Filter = FilterEntry,
            CustomSort = EntryComparer.Instance,
        };
    }

    public ICollectionView EntriesView => _entriesView;

    public ObservableCollection<EntryFilterViewModel> Filters { get; } = [];

    public string SelectedPasswordDisplay =>
        SelectedEntry is not { HasPassword: true } entry ? string.Empty
        : IsPasswordRevealed ? entry.Password
        : MaskedPassword;

    public bool IsListEmpty => VisibleCount == 0;

    public string EmptyListMessage => _items.Count == 0
        ? "Your vault is empty.\nPress Ctrl+N to add your first entry."
        : "No entries match your search.";

    public string VisibleCountText => VisibleCount == 1 ? "1 item" : $"{VisibleCount} items";

    public void OnNavigatedTo(object? parameter) => Reload(selectId: null);

    public void OnNavigatedFrom()
    {
        _statusCancellation?.Cancel();
        SelectedEntry = null;
        _items.ReplaceAll([]);
        Filters.Clear();
    }

    partial void OnSelectedFilterChanged(EntryFilterViewModel? value) => RefreshView();

    partial void OnSearchTextChanged(string value)
    {
        _searchTerms = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        RefreshView();
    }

    partial void OnSelectedEntryChanged(EntryItemViewModel? value)
    {
        IsPasswordRevealed = false;
        SelectedPasswordStrength = value is { HasPassword: true } ? _strengthEvaluator.Evaluate(value.Password) : null;
    }

    private bool HasSelection() => SelectedEntry is not null;

    private bool CanCopyUsername() => SelectedEntry?.HasUsername == true;

    private bool CanCopyPassword() => SelectedEntry?.HasPassword == true;

    private bool HasUrl() => SelectedEntry?.HasUrl == true;

    [RelayCommand]
    private void AddEntry()
    {
        var editor = _viewModels.Create<EntryEditorViewModel>();
        string category = SelectedFilter?.Kind == EntryFilterKind.Category ? SelectedFilter.Name : string.Empty;
        editor.InitializeForNew(GetCategories(), category, SelectedFilter?.Kind == EntryFilterKind.Favorites, draft => _entries.AddEntryAsync(draft));

        if (_dialogs.ShowDialog(editor) == true && editor.SavedEntryId is { } id)
        {
            SearchText = string.Empty;
            Reload(id);
            ShowStatus("Entry added.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EditEntry()
    {
        if (SelectedEntry is not { } item)
        {
            return;
        }

        var editor = _viewModels.Create<EntryEditorViewModel>();
        editor.InitializeForEdit(item.Entry, GetCategories(), draft => _entries.UpdateEntryAsync(item.Id, draft));

        if (_dialogs.ShowDialog(editor) == true)
        {
            Reload(item.Id);
            ShowStatus("Changes saved.");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteEntryAsync()
    {
        if (SelectedEntry is not { } item)
        {
            return;
        }

        bool confirmed = _dialogs.Confirm(
            "Delete entry?",
            $"\"{item.Title}\" will be permanently deleted from your vault. This cannot be undone.",
            "Delete",
            isDestructive: true);

        if (!confirmed)
        {
            return;
        }

        await RunSafelyAsync("delete the entry", async () =>
        {
            await _entries.DeleteEntryAsync(item.Id);
            Reload(selectId: null);
            ShowStatus("Entry deleted.");
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedEntry is not { } item)
        {
            return;
        }

        await RunSafelyAsync("update the entry", async () =>
        {
            await _entries.SetFavoriteAsync(item.Id, !item.IsFavorite);
            Reload(item.Id);
        });
    }

    [RelayCommand(CanExecute = nameof(CanCopyUsername))]
    private void CopyUsername()
    {
        if (SelectedEntry is { HasUsername: true } item)
        {
            CopyToClipboard(item.Username, "Username");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyPassword))]
    private void CopyPassword()
    {
        if (SelectedEntry is { HasPassword: true } item)
        {
            CopyToClipboard(item.Password, "Password");
        }
    }

    [RelayCommand(CanExecute = nameof(HasUrl))]
    private void CopyUrl()
    {
        if (SelectedEntry is { HasUrl: true } item)
        {
            CopyToClipboard(item.Url, "Website");
        }
    }

    [RelayCommand(CanExecute = nameof(HasUrl))]
    private void OpenUrl()
    {
        if (SelectedEntry is { HasUrl: true } item && !_shell.TryOpenWebsite(item.Url))
        {
            ShowStatus("This website address can't be opened.");
        }
    }

    [RelayCommand]
    private void Lock() => _lifecycle.Lock(LockReason.User);

    [RelayCommand]
    private void OpenGenerator() => _dialogs.ShowDialog(_viewModels.Create<PasswordGeneratorViewModel>());

    [RelayCommand]
    private void OpenSettings()
    {
        if (_dialogs.ShowDialog(_viewModels.Create<SettingsViewModel>()) == true)
        {
            ShowStatus("Settings saved.");
        }
    }

    [RelayCommand]
    private void ChangeMasterPassword()
    {
        if (_dialogs.ShowDialog(_viewModels.Create<ChangeMasterPasswordViewModel>()) == true)
        {
            ShowStatus("Master password changed. Your vault was re-encrypted.");
        }
    }

    private void Reload(Guid? selectId)
    {
        Guid? keep = selectId ?? SelectedEntry?.Id;
        IReadOnlyList<VaultEntry> entries = _entries.GetEntries();

        _items.ReplaceAll(entries.Select(e => new EntryItemViewModel(e)));
        RebuildFilters(entries);
        RefreshView();

        SelectedEntry = keep is { } id ? _items.FirstOrDefault(i => i.Id == id && FilterEntry(i)) : null;
    }

    private void RebuildFilters(IReadOnlyList<VaultEntry> entries)
    {
        string? selectedKey = SelectedFilter?.Key;
        var filters = new List<EntryFilterViewModel>
        {
            EntryFilterViewModel.All(entries.Count),
            EntryFilterViewModel.Favorites(entries.Count(e => e.IsFavorite)),
        };

        filters.AddRange(entries
            .Where(e => e.Category.Length > 0)
            .GroupBy(e => e.Category, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => EntryFilterViewModel.ForCategory(g.Key, g.Count())));

        Filters.Clear();
        foreach (EntryFilterViewModel filter in filters)
        {
            Filters.Add(filter);
        }

        SelectedFilter = filters.FirstOrDefault(f => f.Key == selectedKey) ?? filters[0];
    }

    private void RefreshView()
    {
        _entriesView.Refresh();
        VisibleCount = _entriesView.Count;

        if (SelectedEntry is not null && !FilterEntry(SelectedEntry))
        {
            SelectedEntry = null;
        }
    }

    private bool FilterEntry(object candidate) =>
        candidate is EntryItemViewModel item
        && (SelectedFilter?.Includes(item) ?? true)
        && item.Matches(_searchTerms);

    private List<string> GetCategories() => _items
        .Select(i => i.Category)
        .Where(c => c.Length > 0)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private void CopyToClipboard(string value, string what)
    {
        if (!_clipboard.TryCopy(value, out TimeSpan? clearsAfter))
        {
            ShowStatus("The clipboard is in use by another application. Please try again.");
            return;
        }

        ShowStatus(clearsAfter is { } delay
            ? $"{what} copied. The clipboard will be cleared in {delay.TotalSeconds:0} seconds."
            : $"{what} copied.");
    }

    private async Task RunSafelyAsync(string action, Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to {Action}.", action);
            _dialogs.ShowError("Something went wrong", $"Could not {action}. {ex.Message}");
        }
    }

    private void ShowStatus(string message)
    {
        _statusCancellation?.Cancel();
        _statusCancellation?.Dispose();
        _statusCancellation = new CancellationTokenSource();
        StatusMessage = message;
        _ = HideStatusLaterAsync(_statusCancellation.Token);
    }

    private async Task HideStatusLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            StatusMessage = null;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class EntryComparer : IComparer
    {
        public static readonly EntryComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            var left = (EntryItemViewModel?)x;
            var right = (EntryItemViewModel?)y;
            return StringComparer.CurrentCultureIgnoreCase.Compare(left?.Title, right?.Title);
        }
    }
}
