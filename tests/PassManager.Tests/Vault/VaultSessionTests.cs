using PassManager.Core.Cryptography;
using PassManager.Core.Security;
using PassManager.Core.Vault;
using PassManager.Infrastructure.Storage;
using PassManager.Tests.TestSupport;

namespace PassManager.Tests.Vault;

public sealed class VaultSessionTests
{
    private const string MasterPassword = "Correct-Horse-Battery-42";
    private readonly InMemoryVaultStore _store = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_vault_starts_unlocked_and_empty()
    {
        using VaultSession session = TestFactory.CreateSession(_store);
        Assert.Equal(VaultState.NotCreated, session.State);

        using (SensitiveBytes password = TestFactory.Password(MasterPassword))
        {
            await session.CreateAsync(password, Ct);
        }

        Assert.Equal(VaultState.Unlocked, session.State);
        Assert.Empty(session.GetEntries());
        Assert.True(_store.Exists());
    }

    [Fact]
    public async Task Entries_survive_lock_and_unlock()
    {
        using VaultSession session = await CreateVaultAsync();
        VaultEntry github = await session.AddEntryAsync(TestFactory.Draft("GitHub", "gh-Secret-1"), Ct);
        await session.AddEntryAsync(TestFactory.Draft("Bank"), Ct);

        session.Lock(LockReason.User);
        Assert.Equal(VaultState.Locked, session.State);

        using (SensitiveBytes password = TestFactory.Password(MasterPassword))
        {
            await session.UnlockAsync(password, Ct);
        }

        IReadOnlyList<VaultEntry> entries = session.GetEntries();
        Assert.Equal(2, entries.Count);
        Assert.Equal("gh-Secret-1", entries.Single(e => e.Id == github.Id).Password);
    }

    [Fact]
    public async Task A_new_session_can_open_the_persisted_vault()
    {
        using (VaultSession first = await CreateVaultAsync())
        {
            await first.AddEntryAsync(TestFactory.Draft("Mail"), Ct);
        }

        using VaultSession second = TestFactory.CreateSession(_store);
        Assert.Equal(VaultState.Locked, second.State);

        using SensitiveBytes password = TestFactory.Password(MasterPassword);
        await second.UnlockAsync(password, Ct);

        Assert.Equal("Mail", Assert.Single(second.GetEntries()).Title);
    }

    [Fact]
    public async Task Wrong_password_keeps_the_vault_locked()
    {
        using VaultSession session = await CreateVaultAsync();
        session.Lock(LockReason.User);

        using SensitiveBytes wrong = TestFactory.Password("not-the-password");
        await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => session.UnlockAsync(wrong, Ct));

        Assert.False(session.IsUnlocked);
        Assert.Throws<VaultLockedException>(() => session.GetEntries());
    }

    [Fact]
    public async Task Data_operations_require_an_unlocked_vault()
    {
        using VaultSession session = await CreateVaultAsync();
        session.Lock(LockReason.Inactivity);

        await Assert.ThrowsAsync<VaultLockedException>(() => session.AddEntryAsync(TestFactory.Draft("x"), Ct));
    }

    [Fact]
    public async Task Update_favorite_and_delete_are_persisted()
    {
        using VaultSession session = await CreateVaultAsync();
        VaultEntry entry = await session.AddEntryAsync(TestFactory.Draft("Old title"), Ct);

        VaultEntry updated = await session.UpdateEntryAsync(entry.Id, TestFactory.Draft("  New title  "), Ct);
        await session.SetFavoriteAsync(entry.Id, isFavorite: true, Ct);

        VaultEntry stored = Assert.Single(session.GetEntries());
        Assert.Equal("New title", stored.Title);
        Assert.True(stored.IsFavorite);
        Assert.Equal(entry.CreatedAt, stored.CreatedAt);
        Assert.True(updated.ModifiedAt >= entry.ModifiedAt);

        await session.DeleteEntryAsync(entry.Id, Ct);
        Assert.Empty(session.GetEntries());
    }

    [Fact]
    public async Task Invalid_drafts_are_rejected()
    {
        using VaultSession session = await CreateVaultAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => session.AddEntryAsync(new EntryDraft { Title = "   " }, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.AddEntryAsync(new EntryDraft { Title = "Evil", Url = "javascript:alert(1)" }, Ct));
    }

    [Fact]
    public async Task Failed_write_does_not_change_in_memory_state()
    {
        using VaultSession session = await CreateVaultAsync();
        _store.FailWrites = true;

        await Assert.ThrowsAsync<IOException>(() => session.AddEntryAsync(TestFactory.Draft("Lost"), Ct));

        Assert.Empty(session.GetEntries());
    }

    [Fact]
    public async Task Changing_the_master_password_rekeys_the_vault()
    {
        using VaultSession session = await CreateVaultAsync();
        await session.AddEntryAsync(TestFactory.Draft("Keep me"), Ct);
        byte[] before = _store.Content!;

        using (SensitiveBytes current = TestFactory.Password(MasterPassword))
        using (SensitiveBytes next = TestFactory.Password("Brand-New-Passphrase-99"))
        {
            await session.ChangeMasterPasswordAsync(current, next, Ct);
        }

        VaultEnvelope oldEnvelope = new BinaryVaultEnvelopeCodec().Decode(before);
        VaultEnvelope newEnvelope = new BinaryVaultEnvelopeCodec().Decode(_store.Content!);
        Assert.NotEqual(oldEnvelope.Header.Kdf.Salt.ToArray(), newEnvelope.Header.Kdf.Salt.ToArray());

        session.Lock(LockReason.User);
        using (SensitiveBytes old = TestFactory.Password(MasterPassword))
        {
            await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => session.UnlockAsync(old, Ct));
        }

        using (SensitiveBytes next = TestFactory.Password("Brand-New-Passphrase-99"))
        {
            await session.UnlockAsync(next, Ct);
        }

        Assert.Equal("Keep me", Assert.Single(session.GetEntries()).Title);
    }

    [Fact]
    public async Task Changing_the_master_password_requires_the_current_password()
    {
        using VaultSession session = await CreateVaultAsync();
        int writes = _store.WriteCount;

        using SensitiveBytes wrong = TestFactory.Password("wrong-current");
        using SensitiveBytes next = TestFactory.Password("Brand-New-Passphrase-99");
        await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => session.ChangeMasterPasswordAsync(wrong, next, Ct));

        Assert.Equal(writes, _store.WriteCount);
    }

    [Fact]
    public async Task Creating_over_an_existing_vault_is_refused()
    {
        using VaultSession session = await CreateVaultAsync();
        using SensitiveBytes password = TestFactory.Password(MasterPassword);

        await Assert.ThrowsAsync<VaultAlreadyExistsException>(() => session.CreateAsync(password, Ct));
    }

    [Fact]
    public async Task Lock_raises_state_changed_with_reason()
    {
        using VaultSession session = await CreateVaultAsync();
        VaultStateChangedEventArgs? raised = null;
        session.StateChanged += (_, e) => raised = e;

        session.Lock(LockReason.SessionLocked);

        Assert.NotNull(raised);
        Assert.Equal(VaultState.Locked, raised.State);
        Assert.Equal(LockReason.SessionLocked, raised.LockReason);
    }

    [Fact]
    public async Task Weak_kdf_parameters_are_upgraded_on_unlock()
    {
        using (VaultSession weak = await CreateVaultAsync())
        {
            await weak.AddEntryAsync(TestFactory.Draft("Survives upgrade"), Ct);
        }

        var stronger = new VaultSecurityOptions { MemoryKiB = 2048, Iterations = 2, Parallelism = 1 };
        using VaultSession session = TestFactory.CreateSession(_store, stronger);
        using (SensitiveBytes password = TestFactory.Password(MasterPassword))
        {
            await session.UnlockAsync(password, Ct);
        }

        KdfParameters kdf = new BinaryVaultEnvelopeCodec().Decode(_store.Content!).Header.Kdf;
        Assert.Equal(2048, kdf.MemoryKiB);
        Assert.Equal(2, kdf.Iterations);
        Assert.Equal("Survives upgrade", Assert.Single(session.GetEntries()).Title);
    }

    private async Task<VaultSession> CreateVaultAsync()
    {
        VaultSession session = TestFactory.CreateSession(_store);
        using SensitiveBytes password = TestFactory.Password(MasterPassword);
        await session.CreateAsync(password, Ct);
        return session;
    }
}
