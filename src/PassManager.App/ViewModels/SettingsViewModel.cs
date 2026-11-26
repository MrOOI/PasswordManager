using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassManager.App.Services;
using PassManager.Core.Settings;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

public sealed record DurationOption(int Value, string Label);

public sealed partial class SettingsViewModel : DialogViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IShellLauncher _shell;

    [ObservableProperty]
    private DurationOption? _selectedAutoLock;

    [ObservableProperty]
    private DurationOption? _selectedClipboardClear;

    [ObservableProperty]
    private bool _lockOnSessionLock;

    [ObservableProperty]
    private bool _lockOnMinimize;

    [ObservableProperty]
    private bool _hideFromScreenCapture;

    [ObservableProperty]
    private string? _errorMessage;

    public SettingsViewModel(ISettingsService settings, IVaultLifecycle vault, IShellLauncher shell)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        ArgumentNullException.ThrowIfNull(vault);

        AppSettings current = settings.Current;
        VaultLocation = vault.Location;

        AutoLockOptions = WithCurrent(
            [new(0, "Never"), new(1, "After 1 minute"), new(2, "After 2 minutes"), new(5, "After 5 minutes"),
             new(10, "After 10 minutes"), new(15, "After 15 minutes"), new(30, "After 30 minutes"), new(60, "After 1 hour")],
            current.AutoLockMinutes,
            v => $"After {v} minutes");

        ClipboardOptions = WithCurrent(
            [new(0, "Never"), new(10, "After 10 seconds"), new(20, "After 20 seconds"), new(30, "After 30 seconds"),
             new(60, "After 1 minute"), new(90, "After 90 seconds")],
            current.ClipboardClearSeconds,
            v => $"After {v} seconds");

        SelectedAutoLock = AutoLockOptions.First(o => o.Value == current.AutoLockMinutes);
        SelectedClipboardClear = ClipboardOptions.First(o => o.Value == current.ClipboardClearSeconds);
        LockOnSessionLock = current.LockOnSessionLock;
        LockOnMinimize = current.LockOnMinimize;
        HideFromScreenCapture = current.HideFromScreenCapture;
    }

    public override string WindowTitle => "Settings";

    public IReadOnlyList<DurationOption> AutoLockOptions { get; }

    public IReadOnlyList<DurationOption> ClipboardOptions { get; }

    public string VaultLocation { get; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var updated = _settings.Current with
        {
            AutoLockMinutes = SelectedAutoLock?.Value ?? _settings.Current.AutoLockMinutes,
            ClipboardClearSeconds = SelectedClipboardClear?.Value ?? _settings.Current.ClipboardClearSeconds,
            LockOnSessionLock = LockOnSessionLock,
            LockOnMinimize = LockOnMinimize,
            HideFromScreenCapture = HideFromScreenCapture,
        };

        try
        {
            await _settings.SaveAsync(updated);
            RequestClose(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"Settings could not be saved. {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    [RelayCommand]
    private void RevealVaultFile() => _shell.RevealInExplorer(VaultLocation);

    private static List<DurationOption> WithCurrent(List<DurationOption> options, int current, Func<int, string> label)
    {
        if (options.All(o => o.Value != current))
        {
            options.Add(new DurationOption(current, label(current)));
            options.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        return options;
    }
}
