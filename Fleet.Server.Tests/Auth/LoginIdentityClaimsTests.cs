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

    [DataTestMethod]
    [DataRow("https://fleetaidev.ciamlogin.com")]
    [DataRow("ciam")]
    public void NormalizeProvider_RecognizesCiamAsEmailProvider(string provider)
    {
        Assert.AreEqual("Email", LoginIdentityClaims.NormalizeProvider(provider));
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

    [TestMethod]
    public void Resolve_PrefersRealEmailClaimOverExternalIdPreferredUsername()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", externalId),
            new Claim("idp", "google.com"),
            new Claim("email", "real.user@gmail.com"),
            new Claim("preferred_username", $"{externalId}@fleetaidev.onmicrosoft.com"),
            new Claim("name", "Google User"),
        ], "Test"));

        var identity = LoginIdentityClaims.Resolve(principal);

        Assert.AreEqual("Google", identity.Provider);
        Assert.AreEqual("real.user@gmail.com", identity.Email);
    }

    [TestMethod]
    public void Resolve_UsesEmailsArrayClaimBeforeExternalIdPreferredUsername()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", externalId),
            new Claim("emails", "[\"real.user@gmail.com\"]"),
            new Claim("preferred_username", $"{externalId}@fleetaidev.onmicrosoft.com"),
            new Claim("name", "Google User"),
        ], "Test"));

        var identity = LoginIdentityClaims.Resolve(principal);

        Assert.AreEqual("real.user@gmail.com", identity.Email);
    }

    [TestMethod]
    public void Resolve_TreatsExternalIdPreferredUsernameAsMissingEmail()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", externalId),
            new Claim("acr", "https://fleetaidev.ciamlogin.com"),
            new Claim("preferred_username", $"{externalId}@fleetaidev.onmicrosoft.com"),
            new Claim("name", "Google User"),
        ], "Test"));

        var identity = LoginIdentityClaims.Resolve(principal);

        Assert.AreEqual("Email", identity.Provider);
        Assert.AreEqual($"{externalId}@entra.local", identity.Email);
    }

    [DataTestMethod]
    [DataRow("117653d4-fb26-4b34-865d-fa3e6761aa7f@fleetaidev.onmicrosoft.com", false)]
    [DataRow("googleuser_gmail.com#EXT#@fleetaidev.onmicrosoft.com", false)]
    [DataRow("117653d4-fb26-4b34-865d-fa3e6761aa7f@entra.local", false)]
    [DataRow("real.user@gmail.com", true)]
    [DataRow("user@outlook.com", true)]
    public void IsDisplayableEmail_FiltersProviderInternalAddresses(string email, bool expected)
    {
        Assert.AreEqual(
            expected,
            LoginIdentityClaims.IsDisplayableEmail(email, "117653d4-fb26-4b34-865d-fa3e6761aa7f"));
    }
}
