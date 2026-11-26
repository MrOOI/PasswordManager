using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PassManager.App.Composition;
using PassManager.App.Services;
using PassManager.App.ViewModels;
using PassManager.App.Views;
using PassManager.Core.Vault;
using PassManager.Infrastructure.Platform;

namespace PassManager.App;

public partial class App : Application
{
    private SingleInstanceGuard? _instanceGuard;
    private ServiceProvider? _services;
    private ILogger<App>? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceGuard = SingleInstanceGuard.Acquire();
        if (!_instanceGuard.IsFirstInstance)
        {
            _instanceGuard.SignalFirstInstance();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _services = CompositionRoot.Build(AppDataPaths.ResolveDefault());
        _logger = _services.GetRequiredService<ILogger<App>>();
        RegisterGlobalExceptionHandlers();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        _services.GetRequiredService<IAutoLockService>().Attach(mainWindow);
        _instanceGuard.ListenForActivation(() => Dispatcher.BeginInvoke(mainWindow.BringToFront));

        _services.GetRequiredService<MainViewModel>().Start();
        mainWindow.Show();

        _logger.LogInformation("PassManager {Version} started.", typeof(App).Assembly.GetName().Version);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_services is not null)
            {
                // Leave nothing sensitive behind: clipboard first, then wipe keys from memory.
                _services.GetService<IClipboardService>()?.ClearIfOwned();
                _services.GetService<IVaultLifecycle>()?.Lock(LockReason.ApplicationExit);
                _services.GetService<MainViewModel>()?.Dispose();
                _logger?.LogInformation("PassManager exited.");
                _services.Dispose();
            }
        }
        finally
        {
            _instanceGuard?.Dispose();
            base.OnExit(e);
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.LogCritical(args.ExceptionObject as Exception, "Unhandled exception (terminating: {Terminating}).", args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception.");
        MessageBox.Show(
            "An unexpected error occurred. Your vault data is safe.\n\n" + e.Exception.Message,
            "PassManager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
