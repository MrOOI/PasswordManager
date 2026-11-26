using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PassManager.App.ViewModels;
using PassManager.Core.Settings;

namespace PassManager.App.Services;

public interface IDialogService
{
    /// <summary>Shows the window registered for the view model's type as a modal dialog.</summary>
    bool? ShowDialog(DialogViewModelBase viewModel);

    bool Confirm(string title, string message, string confirmText, bool isDestructive = false);

    void ShowError(string title, string message);

    /// <summary>Force-closes every open dialog (used when the vault locks).</summary>
    void CloseAll();
}

/// <summary>Maps dialog view model types to their window types (View-first lookup, ViewModel-first API).</summary>
public sealed class DialogRegistry
{
    private readonly Dictionary<Type, Type> _map = [];

    public DialogRegistry Register<TViewModel, TWindow>()
        where TViewModel : DialogViewModelBase
        where TWindow : Window
    {
        _map[typeof(TViewModel)] = typeof(TWindow);
        return this;
    }

    public Type Resolve(Type viewModelType) =>
        _map.TryGetValue(viewModelType, out Type? windowType)
            ? windowType
            : throw new InvalidOperationException($"No dialog window is registered for {viewModelType.Name}.");
}

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;
    private readonly DialogRegistry _registry;
    private readonly ISettingsService _settings;
    private readonly IScreenCaptureProtection _captureProtection;
    private readonly List<Window> _openDialogs = [];

    public DialogService(IServiceProvider services, DialogRegistry registry, ISettingsService settings, IScreenCaptureProtection captureProtection)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _captureProtection = captureProtection ?? throw new ArgumentNullException(nameof(captureProtection));
    }

    public bool? ShowDialog(DialogViewModelBase viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var window = (Window)_services.GetRequiredService(_registry.Resolve(viewModel.GetType()));
        window.DataContext = viewModel;
        window.Owner = _openDialogs.LastOrDefault() ?? Application.Current.MainWindow;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.SourceInitialized += (_, _) => _captureProtection.Apply(window, _settings.Current.HideFromScreenCapture);

        void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
        {
            try
            {
                window.DialogResult = e.Result;
            }
            catch (InvalidOperationException)
            {
                window.Close();
            }
        }

        viewModel.CloseRequested += OnCloseRequested;
        _openDialogs.Add(window);
        try
        {
            return window.ShowDialog();
        }
        finally
        {
            viewModel.CloseRequested -= OnCloseRequested;
            _openDialogs.Remove(window);
            viewModel.OnClosed();
        }
    }

    public bool Confirm(string title, string message, string confirmText, bool isDestructive = false) =>
        ShowDialog(new MessageDialogViewModel(title, message, confirmText, cancelText: "Cancel", isDestructive)) == true;

    public void ShowError(string title, string message) =>
        ShowDialog(new MessageDialogViewModel(title, message, confirmText: "OK", cancelText: null, isDestructive: false));

    public void CloseAll()
    {
        foreach (Window dialog in _openDialogs.AsEnumerable().Reverse().ToList())
        {
            dialog.Close();
        }
    }
}
