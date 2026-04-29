using System.Security.Claims;

namespace Fleet.Server.Auth;

internal sealed record CurrentLoginIdentity(
    string Provider,
    string ProviderUserId,
    string Email,
    string DisplayName);

internal static class LoginIdentityClaims
{
    public static CurrentLoginIdentity Resolve(ClaimsPrincipal principal)
    {
        var providerUserId = principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? principal.FindFirst("oid")?.Value;

        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new UnauthorizedAccessException("No object identifier claim found in token.");

        var rawEmail = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("preferred_username")?.Value
            ?? string.Empty;
        var email = NormalizeEmail(rawEmail, providerUserId);
        var displayName = principal.FindFirst("name")?.Value
            ?? principal.Identity?.Name
            ?? "User";
        var provider = NormalizeProvider(ResolveProviderClaim(principal), rawEmail);

        return new CurrentLoginIdentity(
            provider,
            providerUserId.Trim(),
            email,
            displayName.Trim());
    }

    private static string? ResolveProviderClaim(ClaimsPrincipal principal)
        => principal.FindFirst("idp")?.Value
            ?? principal.FindFirst("identityprovider")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider")?.Value
            ?? principal.FindFirst("tfp")?.Value
            ?? principal.FindFirst("acr")?.Value;

    public static string NormalizeProvider(string? provider, string? email = null)
    {
        var normalized = (provider ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "Email";

        if (normalized.Contains("google", StringComparison.OrdinalIgnoreCase))
            return "Google";

        if (normalized.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("live.com", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("login.live.com", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("windowslive", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("login.microsoftonline", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("liveid", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("msa", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft";
        }

        if (normalized.Contains("email", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("local", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return "Email";
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            normalized.Contains("ciam", StringComparison.OrdinalIgnoreCase))
        {
            return "Email";
        }

        return normalized;
    }

    public static string NormalizeEmail(string email, string providerUserId)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        var safeProviderUserId = (providerUserId ?? string.Empty).Trim().ToLowerInvariant();
        return $"{safeProviderUserId}@entra.local";
    }
}
