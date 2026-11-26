using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PassManager.App.Services;
using PassManager.App.ViewModels;
using PassManager.App.Views;
using PassManager.Infrastructure;
using PassManager.Infrastructure.Logging;
using PassManager.Infrastructure.Platform;

namespace PassManager.App.Composition;

/// <summary>The single place where abstractions are bound to implementations (Dependency Inversion).</summary>
public static class CompositionRoot
{
    public static ServiceProvider Build(AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new FileLoggerProvider(paths.LogDirectory)));

        services.AddPassManagerCore(paths);

        // UI services
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IClipboardService, SecureClipboardService>();
        services.AddSingleton<IShellLauncher, ShellLauncher>();
        services.AddSingleton<IScreenCaptureProtection, ScreenCaptureProtection>();
        services.AddSingleton<IAutoLockService, AutoLockService>();
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton(new DialogRegistry()
            .Register<EntryEditorViewModel, EntryEditorWindow>()
            .Register<PasswordGeneratorViewModel, PasswordGeneratorWindow>()
            .Register<SettingsViewModel, SettingsWindow>()
            .Register<ChangeMasterPasswordViewModel, ChangeMasterPasswordWindow>()
            .Register<MessageDialogViewModel, MessageDialogWindow>());

        // View models
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<UnlockViewModel>();
        services.AddTransient<VaultViewModel>();
        services.AddTransient<EntryEditorViewModel>();
        services.AddTransient<PasswordGeneratorViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ChangeMasterPasswordViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
        services.AddTransient<EntryEditorWindow>();
        services.AddTransient<PasswordGeneratorWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<ChangeMasterPasswordWindow>();
        services.AddTransient<MessageDialogWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
