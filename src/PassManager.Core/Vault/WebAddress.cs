using System.Diagnostics.CodeAnalysis;

namespace PassManager.Core.Vault;

/// <summary>
/// Validates website addresses stored in entries. Only http(s) is accepted so that opening an entry's
/// website can never launch a local executable, file share or script handler.
/// </summary>
public static class WebAddress
{
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string candidate = input.Trim();
        if (candidate.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        bool isWeb = parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp;
        if (!isWeb || string.IsNullOrEmpty(parsed.Host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
