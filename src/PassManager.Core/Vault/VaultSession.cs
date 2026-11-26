using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PassManager.Core.Cryptography;
using PassManager.Core.Security;

namespace PassManager.Core.Vault;

/// <summary>
/// The application's single source of truth for the vault. Implements both <see cref="IVaultLifecycle"/>
/// and <see cref="IVaultEntries"/> so consumers depend only on the slice they need (Interface Segregation).
/// </summary>
/// <remarks>
/// All operations are serialised through one async gate. Mutations are copy-on-write: a new immutable
/// snapshot is encrypted and persisted first, and only then published in memory, so a failed write can
/// never leave memory and disk out of sync.
/// </remarks>
public sealed class VaultSession : IVaultLifecycle, IVaultEntries, IDisposable
{
    private readonly IVaultStore _store;
    private readonly IVaultEnvelopeCodec _codec;
    private readonly IVaultContentSerializer _serializer;
    private readonly IVaultCryptoService _crypto;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VaultSession> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private UnlockedVault? _unlocked;
    private bool _disposed;

    public VaultSession(
        IVaultStore store,
        IVaultEnvelopeCodec codec,
        IVaultContentSerializer serializer,
        IVaultCryptoService crypto,
        TimeProvider timeProvider,
        ILogger<VaultSession> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler<VaultStateChangedEventArgs>? StateChanged;

    public VaultState State =>
        IsUnlocked ? VaultState.Unlocked
        : _store.Exists() ? VaultState.Locked
        : VaultState.NotCreated;

    public bool IsUnlocked => Volatile.Read(ref _unlocked) is not null;

    public string Location => _store.Location;

    public async Task CreateAsync(SensitiveBytes masterPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_store.Exists())
            {
                throw new VaultAlreadyExistsException(_store.Location);
            }

            VaultKeyMaterial material = await Task
                .Run(() => _crypto.CreateKeyMaterial(masterPassword.ReadOnlySpan), cancellationToken)
                .ConfigureAwait(false);

            var vault = new UnlockedVault(material.Key, material.Header, ImmutableDictionary<Guid, VaultEntry>.Empty);
            try
            {
                await PersistAsync(vault, keepBackup: false, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                material.Key.Dispose();
                throw;
            }

            Volatile.Write(ref _unlocked, vault);
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogInformation("A new vault was created.");
        OnStateChanged(VaultState.Unlocked, lockReason: null);
    }

    public async Task UnlockAsync(SensitiveBytes masterPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_unlocked is not null)
            {
                return;
            }

            byte[] fileContent = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            VaultEnvelope envelope = _codec.Decode(fileContent);

            ProtectedKey key;
            try
            {
                key = await Task
                    .Run(() => _crypto.UnlockKey(masterPassword.ReadOnlySpan, envelope.Header), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidMasterPasswordException)
            {
                _logger.LogWarning("Unlock attempt failed: invalid master password.");
                throw;
            }

            UnlockedVault vault;
            try
            {
                vault = new UnlockedVault(key, envelope.Header, DecryptEntries(key, envelope));
            }
            catch
            {
                key.Dispose();
                throw;
            }

            vault = await UpgradeIfRequiredAsync(vault, masterPassword, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _unlocked, vault);
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogInformation("Vault unlocked.");
        OnStateChanged(VaultState.Unlocked, lockReason: null);
    }

    public async Task ChangeMasterPasswordAsync(SensitiveBytes currentPassword, SensitiveBytes newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentPassword);
        ArgumentNullException.ThrowIfNull(newPassword);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UnlockedVault current = RequireUnlocked();

            // Prove knowledge of the current password; an unattended unlocked session must not be enough.
            using (ProtectedKey verified = await Task
                .Run(() => _crypto.UnlockKey(currentPassword.ReadOnlySpan, current.Header), cancellationToken)
                .ConfigureAwait(false))
            {
            }

            // Brand-new salt AND data key: anyone who learned the old password cannot decrypt future versions.
            VaultKeyMaterial material = await Task
                .Run(() => _crypto.CreateKeyMaterial(newPassword.ReadOnlySpan), cancellationToken)
                .ConfigureAwait(false);

            var rekeyed = new UnlockedVault(material.Key, material.Header, current.Entries);
            try
            {
                await PersistAsync(rekeyed, keepBackup: false, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                material.Key.Dispose();
                throw;
            }

            Volatile.Write(ref _unlocked, rekeyed);
            current.Key.Dispose();
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogInformation("Master password changed and vault re-keyed.");
    }

    public void Lock(LockReason reason)
    {
        UnlockedVault? previous;

        _gate.Wait();
        try
        {
            previous = Interlocked.Exchange(ref _unlocked, null);
        }
        finally
        {
            _gate.Release();
        }

        if (previous is null)
        {
            return;
        }

        previous.Key.Dispose();
        _logger.LogInformation("Vault locked ({Reason}).", reason);
        OnStateChanged(VaultState.Locked, reason);
    }

    public IReadOnlyList<VaultEntry> GetEntries()
    {
        UnlockedVault vault = Volatile.Read(ref _unlocked) ?? throw new VaultLockedException();
        return vault.Entries.Values
            .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.CreatedAt)
            .ToList();
    }

    public Task<VaultEntry> AddEntryAsync(EntryDraft draft, CancellationToken cancellationToken = default)
    {
        EntryDraft normalized = Validate(draft);
        return MutateAsync(
            entries =>
            {
                VaultEntry entry = VaultEntry.Create(normalized, _timeProvider.GetUtcNow());
                return (entries.Add(entry.Id, entry), entry);
            },
            cancellationToken);
    }

    public Task<VaultEntry> UpdateEntryAsync(Guid id, EntryDraft draft, CancellationToken cancellationToken = default)
    {
        EntryDraft normalized = Validate(draft);
        return MutateAsync(
            entries =>
            {
                VaultEntry existing = entries.TryGetValue(id, out VaultEntry? found) ? found : throw new EntryNotFoundException(id);
                VaultEntry updated = existing.Apply(normalized, _timeProvider.GetUtcNow());
                return (entries.SetItem(id, updated), updated);
            },
            cancellationToken);
    }

    public Task<VaultEntry> SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default) =>
        MutateAsync(
            entries =>
            {
                VaultEntry existing = entries.TryGetValue(id, out VaultEntry? found) ? found : throw new EntryNotFoundException(id);
                VaultEntry updated = existing with { IsFavorite = isFavorite };
                return (entries.SetItem(id, updated), updated);
            },
            cancellationToken);

    public Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(
            entries => entries.ContainsKey(id) ? (entries.Remove(id), true) : throw new EntryNotFoundException(id),
            cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Exchange(ref _unlocked, null)?.Key.Dispose();
        _gate.Dispose();
    }

    private static EntryDraft Validate(EntryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        IReadOnlyList<string> errors = draft.GetValidationErrors();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(draft));
        }

        return draft.Normalize();
    }

    private async Task<TResult> MutateAsync<TResult>(
        Func<ImmutableDictionary<Guid, VaultEntry>, (ImmutableDictionary<Guid, VaultEntry> Entries, TResult Result)> mutation,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UnlockedVault current = RequireUnlocked();
            (ImmutableDictionary<Guid, VaultEntry> entries, TResult result) = mutation(current.Entries);

            UnlockedVault next = current with { Entries = entries };
            await PersistAsync(next, keepBackup: true, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _unlocked, next);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private UnlockedVault RequireUnlocked()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _unlocked ?? throw new VaultLockedException();
    }

    private ImmutableDictionary<Guid, VaultEntry> DecryptEntries(ProtectedKey key, VaultEnvelope envelope)
    {
        using SensitiveBytes plaintext = _crypto.DecryptPayload(key, envelope.Header, envelope.Payload);
        VaultDocument document = _serializer.Deserialize(plaintext.ReadOnlySpan);

        ImmutableDictionary<Guid, VaultEntry>.Builder builder = ImmutableDictionary.CreateBuilder<Guid, VaultEntry>();
        foreach (VaultEntry entry in document.Entries)
        {
            if (!builder.TryAdd(entry.Id, entry))
            {
                throw new VaultCorruptedException("The vault contains duplicate entries.");
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Transparently migrates vaults created with weaker KDF parameters to the current policy.</summary>
    private async Task<UnlockedVault> UpgradeIfRequiredAsync(UnlockedVault vault, SensitiveBytes masterPassword, CancellationToken cancellationToken)
    {
        if (!_crypto.RequiresUpgrade(vault.Header))
        {
            return vault;
        }

        VaultKeyMaterial material = await Task
            .Run(() => _crypto.CreateKeyMaterial(masterPassword.ReadOnlySpan), cancellationToken)
            .ConfigureAwait(false);

        var upgraded = new UnlockedVault(material.Key, material.Header, vault.Entries);
        try
        {
            await PersistAsync(upgraded, keepBackup: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal: keep working with the existing (still valid) key and retry on the next unlock.
            material.Key.Dispose();
            _logger.LogWarning(ex, "Upgrading the vault key derivation parameters failed.");
            return vault;
        }

        vault.Key.Dispose();
        _logger.LogInformation("Vault key derivation parameters upgraded to the current security policy.");
        return upgraded;
    }

    private async Task PersistAsync(UnlockedVault vault, bool keepBackup, CancellationToken cancellationToken)
    {
        var document = new VaultDocument(
            VaultDocument.CurrentSchemaVersion,
            vault.Entries.Values.OrderBy(e => e.CreatedAt).ToList());

        byte[] plaintext = _serializer.Serialize(document);
        byte[] fileContent;
        try
        {
            SealedData payload = _crypto.EncryptPayload(vault.Key, vault.Header, plaintext);
            fileContent = _codec.Encode(new VaultEnvelope(vault.Header, payload));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        await _store.WriteAsync(fileContent, keepBackup, cancellationToken).ConfigureAwait(false);
    }

    private void OnStateChanged(VaultState state, LockReason? lockReason) =>
        StateChanged?.Invoke(this, new VaultStateChangedEventArgs(state, lockReason));

    private sealed record UnlockedVault(ProtectedKey Key, VaultHeader Header, ImmutableDictionary<Guid, VaultEntry> Entries);
}
