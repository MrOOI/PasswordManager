using CommunityToolkit.Mvvm.ComponentModel;
using PassManager.App.Services;
using PassManager.Core.Vault;

namespace PassManager.App.ViewModels;

/// <summary>
/// Shell view model. Navigation is driven purely by the vault state machine:
/// NotCreated → Setup, Locked → Unlock, Unlocked → Vault. Locking from anywhere (user, timer, OS)
/// therefore always ends on the unlock screen with all secrets released.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IVaultLifecycle _vault;
    private readonly IViewModelFactory _viewModels;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private readonly IClipboardService _clipboard;

    [ObservableProperty]
    private object? _currentViewModel;

    public MainViewModel(
        IVaultLifecycle vault,
        IViewModelFactory viewModels,
        IUiDispatcher dispatcher,
        IDialogService dialogs,
        IClipboardService clipboard)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _viewModels = viewModels ?? throw new ArgumentNullException(nameof(viewModels));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        _vault.StateChanged += OnVaultStateChanged;
    }

    public string Title => "PassManager";

    public void Start() => NavigateTo(_vault.State, lockReason: null);

    public void Dispose()
    {
        _vault.StateChanged -= OnVaultStateChanged;
        (CurrentViewModel as INavigationAware)?.OnNavigatedFrom();
        CurrentViewModel = null;
    }

    private void OnVaultStateChanged(object? sender, VaultStateChangedEventArgs e) =>
        _dispatcher.Post(() =>
        {
            if (e.State == VaultState.Locked)
            {
                // Nothing that may show a secret survives a lock.
                _dialogs.CloseAll();
                _clipboard.ClearIfOwned();
            }

            NavigateTo(e.State, e.LockReason);
        });

    private void NavigateTo(VaultState state, LockReason? lockReason)
    {
        if (state == VaultState.Unlocked && CurrentViewModel is VaultViewModel)
        {
            return;
        }

        object next = state switch
        {
            VaultState.NotCreated => _viewModels.Create<SetupViewModel>(),
            VaultState.Locked => _viewModels.Create<UnlockViewModel>(),
            _ => _viewModels.Create<VaultViewModel>(),
        };

        (next as INavigationAware)?.OnNavigatedTo(lockReason);
        (CurrentViewModel as INavigationAware)?.OnNavigatedFrom();
        CurrentViewModel = next;
    }
}
