using System.Security.Claims;

namespace Fleet.Server.Auth;

public static class ApiScopeAuthorization
{
    public const string DefaultScope = "access_as_user";

    private static readonly string[] ScopeClaimTypes =
    [
        "scp",
        "http://schemas.microsoft.com/identity/claims/scope"
    ];

    public static bool HasRequiredScope(ClaimsPrincipal principal, string requiredScope = DefaultScope)
    {
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(requiredScope))
        {
            return false;
        }

        foreach (var claimType in ScopeClaimTypes)
        {
            foreach (var claim in principal.FindAll(claimType))
            {
                var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
