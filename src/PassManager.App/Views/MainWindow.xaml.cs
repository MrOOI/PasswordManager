using System.Windows;
using PassManager.App.Services;
using PassManager.App.ViewModels;
using PassManager.Core.Settings;

namespace PassManager.App.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly IScreenCaptureProtection _captureProtection;

    public MainWindow(MainViewModel viewModel, ISettingsService settings, IScreenCaptureProtection captureProtection)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _captureProtection = captureProtection ?? throw new ArgumentNullException(nameof(captureProtection));

        InitializeComponent();
        DataContext = viewModel;

        SourceInitialized += (_, _) => ApplyCaptureProtection();
        _settings.SettingsChanged += OnSettingsChanged;
        Closed += (_, _) => _settings.SettingsChanged -= OnSettingsChanged;
    }

    /// <summary>Restores and focuses the window (used when a second instance is launched).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void OnSettingsChanged(object? sender, AppSettings e) => Dispatcher.BeginInvoke(ApplyCaptureProtection);

    private void ApplyCaptureProtection() => _captureProtection.Apply(this, _settings.Current.HideFromScreenCapture);
}
