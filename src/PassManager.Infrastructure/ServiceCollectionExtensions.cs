using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PassManager.Core.Cryptography;
using PassManager.Core.Passwords;
using PassManager.Core.Security;
using PassManager.Core.Settings;
using PassManager.Core.Vault;
using PassManager.Infrastructure.Cryptography;
using PassManager.Infrastructure.Platform;
using PassManager.Infrastructure.Storage;

namespace PassManager.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the domain services and their infrastructure implementations.</summary>
    public static IServiceCollection AddPassManagerCore(
        this IServiceCollection services,
        AppDataPaths paths,
        VaultSecurityOptions? securityOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton(securityOptions ?? VaultSecurityOptions.Default);
        services.AddSingleton(TimeProvider.System);

        // Cryptography: add more IKeyDerivationFunction / IAuthenticatedCipher implementations to extend.
        services.AddSingleton<IMemoryProtector, DpapiMemoryProtector>();
        services.AddSingleton<IKeyDerivationFunction, Argon2idKeyDerivation>();
        services.AddSingleton<IAuthenticatedCipher, AesGcmCipher>();
        services.AddSingleton<IVaultCryptoService, VaultCryptoService>();

        // Persistence
        services.AddSingleton<IVaultEnvelopeCodec, BinaryVaultEnvelopeCodec>();
        services.AddSingleton<IVaultContentSerializer, JsonVaultContentSerializer>();
        services.AddSingleton<IVaultStore>(_ => new FileVaultStore(paths.VaultFile));
        services.AddSingleton<ISettingsService>(sp =>
            new JsonSettingsService(paths.SettingsFile, sp.GetRequiredService<ILogger<JsonSettingsService>>()));

        // Vault session: one instance exposed through two narrow interfaces.
        services.AddSingleton<VaultSession>();
        services.AddSingleton<IVaultLifecycle>(sp => sp.GetRequiredService<VaultSession>());
        services.AddSingleton<IVaultEntries>(sp => sp.GetRequiredService<VaultSession>());

        // Passwords
        services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
        services.AddSingleton<IPasswordStrengthEvaluator, PasswordStrengthEvaluator>();
        services.AddSingleton<IMasterPasswordPolicy, MasterPasswordPolicy>();

        return services;
    }
}
