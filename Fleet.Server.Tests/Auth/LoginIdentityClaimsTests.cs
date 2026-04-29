using System.Security.Claims;
using Fleet.Server.Auth;

namespace Fleet.Server.Tests.Auth;

[TestClass]
public class LoginIdentityClaimsTests
{
    [DataTestMethod]
    [DataRow("https://login.live.com")]
    [DataRow("login.live.com")]
    [DataRow("live.com")]
    [DataRow("liveid")]
    [DataRow("msa")]
    [DataRow("MSA-MicrosoftAccount-OpenIdConnect")]
    public void NormalizeProvider_RecognizesMicrosoftAccountProviders(string provider)
    {
        Assert.AreEqual("Microsoft", LoginIdentityClaims.NormalizeProvider(provider));
    }

    [TestMethod]
    public void Resolve_MapsMicrosoftAccountIdentityProviderClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", "microsoft-oid"),
            new Claim("identityprovider", "https://login.live.com"),
            new Claim("preferred_username", "user@outlook.com"),
            new Claim("name", "Microsoft User"),
        ], "Test"));

        var identity = LoginIdentityClaims.Resolve(principal);

        Assert.AreEqual("Microsoft", identity.Provider);
        Assert.AreEqual("microsoft-oid", identity.ProviderUserId);
        Assert.AreEqual("user@outlook.com", identity.Email);
        Assert.AreEqual("Microsoft User", identity.DisplayName);
    }
}
