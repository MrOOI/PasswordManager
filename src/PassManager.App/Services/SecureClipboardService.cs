using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PassManager.Core.Settings;

namespace PassManager.App.Services;

public interface IClipboardService
{
    /// <summary>Copies a secret. Returns false if the clipboard is locked by another application.</summary>
    /// <param name="clearsAfter">When the clipboard will be wiped automatically, or null if never.</param>
    bool TryCopy(string text, out TimeSpan? clearsAfter);

    /// <summary>Clears the clipboard if (and only if) it still holds the value this application put there.</summary>
    void ClearIfOwned();
}

/// <summary>
/// Clipboard access hardened for secrets:
/// <list type="bullet">
/// <item>Excluded from Windows clipboard history (Win+V), cloud clipboard sync and clipboard monitors.</item>
/// <item>Automatically cleared after a configurable delay, on lock and on exit, without clobbering
/// anything the user copied in the meantime.</item>
/// </list>
/// </summary>
public sealed class SecureClipboardService : IClipboardService, IDisposable
{
    private const int MaxAttempts = 5;

    private readonly ISettingsService _settings;
    private readonly ILogger<SecureClipboardService> _logger;
    private readonly DispatcherTimer _clearTimer;
    private string? _ownedText;

    public SecureClipboardService(ISettingsService settings, ILogger<SecureClipboardService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clearTimer = new DispatcherTimer(DispatcherPriority.Background);
        _clearTimer.Tick += OnClearTimerTick;
    }

    public bool TryCopy(string text, out TimeSpan? clearsAfter)
    {
        ArgumentNullException.ThrowIfNull(text);
        clearsAfter = null;

        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        // Documented opt-out formats honoured by Windows clipboard history and cloud clipboard.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));

        if (!TryClipboardAction(() => Clipboard.SetDataObject(data, copy: true)))
        {
            return false;
        }

        _ownedText = text;
        _clearTimer.Stop();

        int seconds = _settings.Current.ClipboardClearSeconds;
        if (seconds > 0)
        {
            clearsAfter = TimeSpan.FromSeconds(seconds);
            _clearTimer.Interval = clearsAfter.Value;
            _clearTimer.Start();
        }

        return true;
    }

    public void ClearIfOwned()
    {
        _clearTimer.Stop();
        string? owned = _ownedText;
        _ownedText = null;
        if (owned is null)
        {
            return;
        }

        TryClipboardAction(() =>
        {
            if (Clipboard.ContainsText() && string.Equals(Clipboard.GetText(), owned, StringComparison.Ordinal))
            {
                Clipboard.Clear();
            }
        });
    }

    public void Dispose()
    {
        _clearTimer.Stop();
        _clearTimer.Tick -= OnClearTimerTick;
    }

    private void OnClearTimerTick(object? sender, EventArgs e) => ClearIfOwned();

    private bool TryClipboardAction(Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return true;
            }
            catch (ExternalException ex) when (attempt < MaxAttempts)
            {
                // Another process holds the clipboard open; back off briefly and retry.
                _logger.LogDebug(ex, "Clipboard busy (attempt {Attempt}).", attempt);
                Thread.Sleep(40 * attempt);
            }
            catch (ExternalException ex)
            {
                _logger.LogWarning(ex, "Clipboard is unavailable.");
                return false;
            }
        }
    }
}
