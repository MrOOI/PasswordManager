namespace PassManager.Core.Vault;

/// <summary>Base type for all expected, user-presentable vault failures.</summary>
public class VaultException : Exception
{
    public VaultException()
    {
    }

    public VaultException(string message)
        : base(message)
    {
    }

    public VaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The master password is wrong (or the key-wrapping header was tampered with).</summary>
public sealed class InvalidMasterPasswordException : VaultException
{
    public InvalidMasterPasswordException()
        : base("The master password is incorrect.")
    {
    }

    public InvalidMasterPasswordException(Exception innerException)
        : base("The master password is incorrect.", innerException)
    {
    }
}

/// <summary>The vault file is damaged or failed integrity verification.</summary>
public sealed class VaultCorruptedException : VaultException
{
    public VaultCorruptedException(string message)
        : base(message)
    {
    }

    public VaultCorruptedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The vault was written by a newer version of the application or uses an unknown algorithm.</summary>
public sealed class UnsupportedVaultFormatException : VaultException
{
    public UnsupportedVaultFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>An operation that needs decrypted data was attempted while the vault is locked.</summary>
public sealed class VaultLockedException : VaultException
{
    public VaultLockedException()
        : base("The vault is locked.")
    {
    }
}

/// <summary>No vault file exists at the configured location.</summary>
public sealed class VaultNotFoundException : VaultException
{
    public VaultNotFoundException(string location)
        : base($"No vault was found at '{location}'.")
    {
    }
}

/// <summary>An attempt was made to create a vault where one already exists.</summary>
public sealed class VaultAlreadyExistsException : VaultException
{
    public VaultAlreadyExistsException(string location)
        : base($"A vault already exists at '{location}'.")
    {
    }
}

/// <summary>The requested entry does not exist.</summary>
public sealed class EntryNotFoundException : VaultException
{
    public EntryNotFoundException(Guid id)
        : base($"Entry '{id}' was not found.")
    {
    }
}
