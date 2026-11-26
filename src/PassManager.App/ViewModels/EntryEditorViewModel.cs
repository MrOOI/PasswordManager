using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PassManager.App.Services;
using PassManager.Core.Passwords;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>Create / edit dialog for a vault entry.</summary>
public sealed partial class EntryEditorViewModel : DialogViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IViewModelFactory _viewModels;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;
    private readonly ILogger<EntryEditorViewModel> _logger;
    private Func<EntryDraft, Task<VaultEntry>>? _save;
    private bool _isNew = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _entryTitle = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private PasswordStrengthResult? _strength;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    public EntryEditorViewModel(
        IDialogService dialogs,
        IViewModelFactory viewModels,
        IPasswordStrengthEvaluator strengthEvaluator,
        ILogger<EntryEditorViewModel> logger)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
        _strengthEvaluator = strengthEvaluator ?? throw new ArgumentNullException(nameof(strengthEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string WindowTitle => _isNew ? "New entry" : "Edit entry";

    public IReadOnlyList<string> Categories { get; private set; } = [];

    public Guid? SavedEntryId { get; private set; }

    public int TitleMaxLength => EntryLimits.TitleMaxLength;

    public int UsernameMaxLength => EntryLimits.UsernameMaxLength;

    public int PasswordMaxLength => EntryLimits.PasswordMaxLength;

    public int UrlMaxLength => EntryLimits.UrlMaxLength;

    public int CategoryMaxLength => EntryLimits.CategoryMaxLength;

    public int NotesMaxLength => EntryLimits.NotesMaxLength;

    public void InitializeForNew(IReadOnlyList<string> categories, string category, bool isFavorite, Func<EntryDraft, Task<VaultEntry>> save)
    {
        _isNew = true;
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Categories = categories;
        Category = category;
        IsFavorite = isFavorite;
        OnPropertyChanged(nameof(WindowTitle));
    }

    public void InitializeForEdit(VaultEntry entry, IReadOnlyList<string> categories, Func<EntryDraft, Task<VaultEntry>> save)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _isNew = false;
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Categories = categories;
        EntryTitle = entry.Title;
        Username = entry.Username;
        Password = entry.Password;
        Url = entry.Url;
        Category = entry.Category;
        Notes = entry.Notes;
        IsFavorite = entry.IsFavorite;
        OnPropertyChanged(nameof(WindowTitle));
    }

    public override void OnClosed()
    {
        // Drop references to the plaintext as soon as the dialog is gone.
        Password = string.Empty;
        _save = null;
    }

    partial void OnPasswordChanged(string value) =>
        Strength = string.IsNullOrEmpty(value) ? null : _strengthEvaluator.Evaluate(value);

    [RelayCommand]
    private void GeneratePassword()
    {
        var generator = _viewModels.Create<PasswordGeneratorViewModel>();
        generator.EnableSelectionMode();

        if (_dialogs.ShowDialog(generator) == true && generator.SelectedPassword is { } generated)
        {
            Password = generated;
        }
    }

    private bool CanSave() => !IsSaving && !string.IsNullOrWhiteSpace(EntryTitle);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_save is null)
        {
            return;
        }

        var draft = new EntryDraft
        {
            Title = EntryTitle,
            Username = Username,
            Password = Password,
            Url = Url,
            Notes = Notes,
            Category = Category,
            IsFavorite = IsFavorite,
        };

        IReadOnlyList<string> errors = draft.GetValidationErrors();
        if (errors.Count > 0)
        {
            ErrorMessage = string.Join(Environment.NewLine, errors);
            return;
        }

        ErrorMessage = null;
        IsSaving = true;
        try
        {
            VaultEntry saved = await _save(draft);
            SavedEntryId = saved.Id;
            RequestClose(true);
        }
        catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogError(ex, "Saving an entry failed.");
            ErrorMessage = $"The entry could not be saved. {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
