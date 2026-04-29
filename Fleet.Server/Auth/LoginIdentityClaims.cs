using System.Security.Claims;
using System.Text.Json;

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

        var normalizedProviderUserId = providerUserId.Trim();
        var rawEmail = ResolveDisplayEmail(principal, normalizedProviderUserId) ?? string.Empty;
        var email = NormalizeEmail(rawEmail, normalizedProviderUserId);
        var displayName = principal.FindFirst("name")?.Value
            ?? principal.Identity?.Name
            ?? "User";
        var provider = NormalizeProvider(ResolveProviderClaim(principal), rawEmail);

        return new CurrentLoginIdentity(
            provider,
            normalizedProviderUserId,
            email,
            displayName.Trim());
    }

    private static string? ResolveProviderClaim(ClaimsPrincipal principal)
        => principal.FindFirst("idp")?.Value
            ?? principal.FindFirst("identityprovider")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider")?.Value
            ?? principal.FindFirst("tfp")?.Value
            ?? principal.FindFirst("acr")?.Value;

    public static string? ResolveDisplayEmail(ClaimsPrincipal principal, string? providerUserId = null)
    {
        foreach (var candidate in ResolveEmailCandidates(principal))
        {
            var normalized = NormalizeEmailCandidate(candidate);
            if (IsDisplayableEmail(normalized, providerUserId))
                return normalized;
        }

        return null;
    }

    private static IEnumerable<string> ResolveEmailCandidates(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.FindAll(ClaimTypes.Email))
        {
            foreach (var candidate in ExpandEmailClaimValue(claim.Value))
                yield return candidate;
        }

        foreach (var claimType in new[]
                 {
                     "email",
                     "emails",
                     "signInNames.emailAddress",
                     "preferred_username",
                     "upn"
                 })
        {
            foreach (var claim in principal.FindAll(claimType))
            {
                foreach (var candidate in ExpandEmailClaimValue(claim.Value))
                    yield return candidate;
            }
        }
    }

    private static IEnumerable<string> ExpandEmailClaimValue(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            yield break;

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            string[]? values = null;
            try
            {
                values = JsonSerializer.Deserialize<string[]>(trimmed);
            }
            catch (JsonException)
            {
                // Some identity providers use a plain string here despite the plural claim name.
            }

            if (values is { Length: > 0 })
            {
                foreach (var candidate in values)
                {
                    if (!string.IsNullOrWhiteSpace(candidate))
                        yield return candidate;
                }

                yield break;
            }
        }

        foreach (var candidate in trimmed.Split(
                     [',', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return candidate;
        }
    }

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

        if (normalized.Contains("ciam", StringComparison.OrdinalIgnoreCase))
        {
            return "Email";
        }

        return normalized;
    }

    public static string NormalizeEmail(string email, string providerUserId)
    {
        var normalized = NormalizeDisplayEmail(email, providerUserId);
        if (normalized is not null)
            return normalized;

        var safeProviderUserId = NormalizeEmailCandidate(providerUserId);
        return $"{safeProviderUserId}@entra.local";
    }

    public static string? NormalizeDisplayEmail(string? email, string? providerUserId = null)
    {
        var normalized = NormalizeEmailCandidate(email);
        return IsDisplayableEmail(normalized, providerUserId)
            ? normalized
            : null;
    }

    public static bool IsDisplayableEmail(string? email, string? providerUserId = null)
    {
        var normalized = NormalizeEmailCandidate(email);
        return LooksLikeEmail(normalized) &&
            !normalized.EndsWith("@entra.local", StringComparison.OrdinalIgnoreCase) &&
            !IsInternalEntraEmail(normalized, providerUserId);
    }

    private static string NormalizeEmailCandidate(string? email)
        => (email ?? string.Empty).Trim().Trim('"').ToLowerInvariant();

    private static bool LooksLikeEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex > 0 && atIndex < email.Length - 1;
    }

    private static bool IsInternalEntraEmail(string email, string? providerUserId)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex < 0)
            return false;

        var localPart = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        if (!domain.Equals("onmicrosoft.com", StringComparison.OrdinalIgnoreCase) &&
            !domain.EndsWith(".onmicrosoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(localPart, out _) ||
            localPart.Contains("#ext#", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(providerUserId) &&
                string.Equals(localPart, providerUserId.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
