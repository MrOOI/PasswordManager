using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PassManager.Core.Settings;

namespace PassManager.Infrastructure.Storage;

/// <summary>Persists <see cref="AppSettings"/> as JSON. A missing or damaged file falls back to defaults.</summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _path;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _current;

    public JsonSettingsService(string path, ILogger<JsonSettingsService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _current = Load();
    }

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Current => Volatile.Read(ref _current);

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AppSettings sanitized = settings.Sanitize();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            string tempPath = _path + ".tmp";
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(sanitized, SettingsJsonContext.Default.AppSettings);
            await File.WriteAllBytesAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _path, overwrite: true);
            Volatile.Write(ref _current, sanitized);
        }
        finally
        {
            _gate.Release();
        }

        SettingsChanged?.Invoke(this, sanitized);
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            byte[] json = File.ReadAllBytes(_path);
            return (JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings()).Sanitize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Settings could not be loaded; defaults are used.");
            return new AppSettings();
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
