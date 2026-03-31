using Fleet.Server.Connections;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class GitHubTokenProtectorTests
{
    [TestMethod]
    public void ProtectAndUnprotect_RoundTripAcrossInstances_WhenStableKeyConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:TokenEncryptionKey"] = "stable-test-key",
            })
            .Build();

        var protectorA = new GitHubTokenProtector(
            DataProtectionProvider.Create("fleet-token-tests-a"),
            configuration);
        var protectorB = new GitHubTokenProtector(
            DataProtectionProvider.Create("fleet-token-tests-b"),
            configuration);

        var protectedToken = protectorA.Protect("ghu_roundtrip");
        var unprotectedToken = protectorB.Unprotect(protectedToken);

        StringAssert.StartsWith(protectedToken, "ghep1:");
        Assert.AreEqual("ghu_roundtrip", unprotectedToken);
    }

    [TestMethod]
    public void Unprotect_ReturnsNull_WhenAspNetDataProtectionPayloadCannotBeRead()
    {
        var writer = new GitHubTokenProtector(DataProtectionProvider.Create("fleet-token-tests-writer"));
        var reader = new GitHubTokenProtector(DataProtectionProvider.Create("fleet-token-tests-reader"));

        var protectedToken = writer.Protect("ghu_dp_only");
        var unprotectedToken = reader.Unprotect(protectedToken);

        Assert.IsNull(unprotectedToken);
    }

    [TestMethod]
    public void Unprotect_ReturnsPlaintext_ForLegacyRows()
    {
        var protector = new GitHubTokenProtector(DataProtectionProvider.Create("fleet-token-tests-legacy"));

        var unprotectedToken = protector.Unprotect("legacy-plaintext-token");

        Assert.AreEqual("legacy-plaintext-token", unprotectedToken);
    }
}
