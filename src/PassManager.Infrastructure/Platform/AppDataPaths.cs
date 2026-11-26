namespace PassManager.Infrastructure.Platform;

/// <summary>Locations of per-user application data.</summary>
public sealed record AppDataPaths(string DataDirectory)
{
    /// <summary>Overrides the data directory (portable installs, automated UI tests).</summary>
    public const string DataDirectoryVariable = "PASSMANAGER_DATA_DIR";

    public string VaultFile => Path.Combine(DataDirectory, "vault.pmv");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static AppDataPaths ResolveDefault()
    {
        string? overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new AppDataPaths(Path.GetFullPath(overridden));
        }

        // %LOCALAPPDATA% is private to the user account (ACL protected) and is not roamed to other machines.
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        return new AppDataPaths(Path.Combine(localAppData, "PassManager"));
    }
}
