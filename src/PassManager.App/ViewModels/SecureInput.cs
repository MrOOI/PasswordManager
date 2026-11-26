using System.Security;

namespace PassManager.App.ViewModels;

internal static class SecureInput
{
    /// <summary>Stores a new <see cref="SecureString"/> copy coming from a PasswordBox and wipes the previous one.</summary>
    public static bool Replace(ref SecureString? field, SecureString? value)
    {
        if (ReferenceEquals(field, value))
        {
            return false;
        }

        field?.Dispose();
        field = value;
        return true;
    }

    public static bool IsEmpty(SecureString? value) => value is null || value.Length == 0;
}
