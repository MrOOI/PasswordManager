using PassManager.Core.Vault;

namespace PassManager.Infrastructure.Storage;

/// <summary>
/// Stores the encrypted vault in a single file. Writes are crash-safe: data goes to a temporary file,
/// is flushed to disk, and then atomically swapped in with <see cref="File.Replace(string, string, string?)"/>.
/// A power cut can therefore never leave a half-written vault behind.
/// </summary>
public sealed class FileVaultStore : IVaultStore
{
    private readonly string _path;
    private readonly string _backupPath;

    public FileVaultStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _backupPath = _path + ".bak";
    }

    public string Location => _path;

    public bool Exists() => File.Exists(_path);

    public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            throw new VaultNotFoundException(_path);
        }

        return await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> content, bool keepBackup, CancellationToken cancellationToken = default)
    {
        string directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Invalid vault path.");
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough | FileOptions.Asynchronous,
            };

            await using (var stream = new FileStream(tempPath, options))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                File.Replace(tempPath, _path, keepBackup ? _backupPath : null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _path);
            }

            if (!keepBackup && File.Exists(_backupPath))
            {
                File.Delete(_backupPath);
            }
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort: a stray temp file only contains ciphertext.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
