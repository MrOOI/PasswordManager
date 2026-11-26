using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassManager.App.Services;
using PassManager.Core.Passwords;

namespace PassManager.App.ViewModels;

/// <summary>Interactive password generator (standalone, or as a picker for the entry editor).</summary>
public sealed partial class PasswordGeneratorViewModel : DialogViewModelBase
{
    private readonly IPasswordGenerator _generator;
    private readonly IPasswordStrengthEvaluator _strengthEvaluator;
    private readonly IClipboardService _clipboard;

    [ObservableProperty]
    private int _length = 20;

    [ObservableProperty]
    private bool _includeUppercase = true;

    [ObservableProperty]
    private bool _includeLowercase = true;

    [ObservableProperty]
    private bool _includeDigits = true;

    [ObservableProperty]
    private bool _includeSymbols = true;

    [ObservableProperty]
    private bool _excludeAmbiguous;

    [ObservableProperty]
    private string _generatedPassword = string.Empty;

    [ObservableProperty]
    private PasswordStrengthResult? _strength;

    [ObservableProperty]
    private string? _statusMessage;

    public PasswordGeneratorViewModel(IPasswordGenerator generator, IPasswordStrengthEvaluator strengthEvaluator, IClipboardService clipboard)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _strengthEvaluator = strengthEvaluator ?? throw new ArgumentNullException(nameof(strengthEvaluator));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        Regenerate();
    }

    public override string WindowTitle => "Password generator";

    public int MinimumLength => 8;

    public int MaximumLength => 64;

    public bool IsSelectionMode { get; private set; }

    public string? SelectedPassword { get; private set; }

    public void EnableSelectionMode()
    {
        IsSelectionMode = true;
        OnPropertyChanged(nameof(IsSelectionMode));
    }

    public override void OnClosed()
    {
        GeneratedPassword = string.Empty;
    }

    partial void OnLengthChanged(int value) => Regenerate();

    partial void OnIncludeUppercaseChanged(bool value) => OnCharacterSetsChanged();

    partial void OnIncludeLowercaseChanged(bool value) => OnCharacterSetsChanged();

    partial void OnIncludeDigitsChanged(bool value) => OnCharacterSetsChanged();

    partial void OnIncludeSymbolsChanged(bool value) => OnCharacterSetsChanged();

    partial void OnExcludeAmbiguousChanged(bool value) => Regenerate();

    private void OnCharacterSetsChanged()
    {
        // At least one character set must stay selected.
        if (!IncludeUppercase && !IncludeLowercase && !IncludeDigits && !IncludeSymbols)
        {
            IncludeLowercase = true;
            return;
        }

        Regenerate();
    }

    [RelayCommand]
    private void Regenerate()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = Math.Clamp(Length, MinimumLength, MaximumLength),
            IncludeUppercase = IncludeUppercase,
            IncludeLowercase = IncludeLowercase,
            IncludeDigits = IncludeDigits,
            IncludeSymbols = IncludeSymbols,
            ExcludeAmbiguous = ExcludeAmbiguous,
        };

        GeneratedPassword = _generator.Generate(options);
        Strength = _strengthEvaluator.Evaluate(GeneratedPassword);
        StatusMessage = null;
    }

    [RelayCommand]
    private void Copy()
    {
        StatusMessage = _clipboard.TryCopy(GeneratedPassword, out TimeSpan? clearsAfter)
            ? clearsAfter is { } delay ? $"Copied. Clipboard clears in {delay.TotalSeconds:0} s." : "Copied."
            : "The clipboard is busy. Please try again.";
    }

    [RelayCommand]
    private void UsePassword()
    {
        SelectedPassword = GeneratedPassword;
        RequestClose(true);
    }

    [RelayCommand]
    private void Dismiss() => RequestClose(false);
}
