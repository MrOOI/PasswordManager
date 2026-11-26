namespace PassManager.Core.Vault;

public enum VaultState
{
    NotCreated,
    Locked,
    Unlocked,
}

public enum LockReason
{
    User,
    Inactivity,
    SessionLocked,
    SystemSuspend,
    Minimized,
    ApplicationExit,
}

public sealed class VaultStateChangedEventArgs : EventArgs
{
    public VaultStateChangedEventArgs(VaultState state, LockReason? lockReason)
    {
        State = state;
        LockReason = lockReason;
    }

    public VaultState State { get; }

    /// <summary>Set when <see cref="State"/> is <see cref="VaultState.Locked"/>.</summary>
    public LockReason? LockReason { get; }
}
