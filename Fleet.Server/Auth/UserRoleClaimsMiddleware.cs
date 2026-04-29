using System.Security.Claims;

namespace Fleet.Server.Auth;

/// <summary>
/// Enriches authenticated requests with an app-local role claim from UserProfiles.
/// This lets infrastructure components (e.g., rate limiting) make role-based decisions.
/// </summary>
public class UserRoleClaimsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuthRepository authRepository)
    {
        var principal = context.User;
        var identity = principal.Identity as ClaimsIdentity;

        if (identity is not null &&
            identity.IsAuthenticated &&
            !principal.HasClaim(c => c.Type == FleetClaimTypes.AppRole))
        {
            var oid = principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? principal.FindFirst("oid")?.Value;

            if (!string.IsNullOrWhiteSpace(oid))
            {
                var provider = LoginIdentityClaims.Resolve(principal).Provider;
                var loginIdentity = await authRepository.GetLoginIdentityAsync(provider, oid);
                var user = loginIdentity?.UserProfile
                    ?? await authRepository.GetByEntraObjectIdAsync(oid);
                var role = UserRoles.Normalize(user?.Role);
                identity.AddClaim(new Claim(FleetClaimTypes.AppRole, role));
                if (user is not null)
                {
                    identity.AddClaim(new Claim(FleetClaimTypes.UserId, user.Id.ToString()));
                }
            }
        }

        await next(context);
    }
}
