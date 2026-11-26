using PassManager.Core.Vault;
using PassManager.Infrastructure.Storage;

namespace PassManager.Tests.Storage;

public sealed class FileVaultStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PassManagerTests", Guid.NewGuid().ToString("N"));
    private readonly FileVaultStore _store;

    public FileVaultStoreTests()
    {
        _store = new FileVaultStore(Path.Combine(_directory, "vault.pmv"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Write_then_read_round_trips()
    {
        await _store.WriteAsync(new byte[] { 1, 2, 3 }, keepBackup: true, Ct);

        Assert.True(_store.Exists());
        Assert.Equal(new byte[] { 1, 2, 3 }, await _store.ReadAsync(Ct));
    }

    [Fact]
    public async Task Overwrite_keeps_previous_version_as_backup()
    {
        await _store.WriteAsync(new byte[] { 1 }, keepBackup: true, Ct);
        await _store.WriteAsync(new byte[] { 2 }, keepBackup: true, Ct);

        Assert.Equal(new byte[] { 2 }, await _store.ReadAsync(Ct));
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(_store.Location + ".bak", Ct));
    }

    [Fact]
    public async Task Writing_without_backup_removes_stale_backups()
    {
        await _store.WriteAsync(new byte[] { 1 }, keepBackup: true, Ct);
        await _store.WriteAsync(new byte[] { 2 }, keepBackup: true, Ct);
        await _store.WriteAsync(new byte[] { 3 }, keepBackup: false, Ct);

        Assert.False(File.Exists(_store.Location + ".bak"));
    }

    [Fact]
    public async Task No_temporary_files_are_left_behind()
    {
        await _store.WriteAsync(new byte[] { 1 }, keepBackup: true, Ct);
        await _store.WriteAsync(new byte[] { 2 }, keepBackup: true, Ct);

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Reading_a_missing_vault_throws()
    {
        await Assert.ThrowsAsync<VaultNotFoundException>(() => _store.ReadAsync(Ct));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
