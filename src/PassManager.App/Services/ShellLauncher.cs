using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using PassManager.Core.Vault;

namespace PassManager.App.Services;

public interface IShellLauncher
{
    /// <summary>Opens an http(s) address in the default browser. Anything else is refused.</summary>
    bool TryOpenWebsite(string address);

    /// <summary>Shows a file in Windows Explorer.</summary>
    void RevealInExplorer(string path);
}

public sealed class ShellLauncher : IShellLauncher
{
    private readonly ILogger<ShellLauncher> _logger;

    public ShellLauncher(ILogger<ShellLauncher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool TryOpenWebsite(string address)
    {
        // Security: never ShellExecute arbitrary entry content (file://, UNC paths, custom protocol handlers).
        if (!WebAddress.TryNormalize(address, out Uri? uri))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Opening the website failed.");
            return false;
        }
    }

    public void RevealInExplorer(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        if (File.Exists(path))
        {
            startInfo.ArgumentList.Add("/select," + path);
        }
        else
        {
            startInfo.ArgumentList.Add(Path.GetDirectoryName(path) ?? path);
        }

        Process.Start(startInfo)?.Dispose();
    }
}
