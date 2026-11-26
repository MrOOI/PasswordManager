using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PassManager.Infrastructure.Logging;

/// <summary>
/// Minimal daily-rolling file logger for diagnostics. The application never logs secrets
/// (entries override ToString and only operational events are written).
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetentionDays = 14;

    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly Lock _sync = new();

    public FileLoggerProvider(string directory, LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _minimumLevel = minimumLevel;
        DeleteExpiredLogs();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string message)
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                string file = Path.Combine(_directory, $"passmanager-{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
                File.AppendAllText(file, message + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never crash the application.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void DeleteExpiredLogs()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            DateTime threshold = DateTime.UtcNow.AddDays(-RetentionDays);
            foreach (string file in Directory.EnumerateFiles(_directory, "passmanager-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < threshold)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {category}: {formatter(state, exception)}");

            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            provider.Write(line);
        }
    }
}
