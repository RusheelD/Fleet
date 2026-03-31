using System.Security.Claims;
using Fleet.Server.Auth;

namespace Fleet.Server.Tests.Auth;

[TestClass]
public class ApiScopeAuthorizationTests
{
    [TestMethod]
    public void HasRequiredScope_ReturnsTrue_WhenScpClaimContainsAccessAsUser()
    {
        var principal = CreatePrincipal(new Claim("scp", "access_as_user"));

        Assert.IsTrue(ApiScopeAuthorization.HasRequiredScope(principal));
    }

    [TestMethod]
    public void HasRequiredScope_ReturnsTrue_WhenLegacyScopeClaimContainsAccessAsUserAmongOtherScopes()
    {
        var principal = CreatePrincipal(new Claim(
            "http://schemas.microsoft.com/identity/claims/scope",
            "profile access_as_user email"));

        Assert.IsTrue(ApiScopeAuthorization.HasRequiredScope(principal));
    }

    [TestMethod]
    public void HasRequiredScope_ReturnsFalse_WhenRequiredScopeIsMissing()
    {
        var principal = CreatePrincipal(new Claim("scp", "openid profile"));

        Assert.IsFalse(ApiScopeAuthorization.HasRequiredScope(principal));
    }

    [TestMethod]
    public void HasRequiredScope_ReturnsFalse_ForUnauthenticatedPrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.IsFalse(ApiScopeAuthorization.HasRequiredScope(principal));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
