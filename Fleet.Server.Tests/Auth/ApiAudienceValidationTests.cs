using Fleet.Server.Auth;

namespace Fleet.Server.Tests.Auth;

[TestClass]
public class ApiAudienceValidationTests
{
    [TestMethod]
    public void ResolveValidAudiences_Includes_ClientId_And_ApiUri_Form()
    {
        var clientId = "73df32d0-82b8-4eba-bc4a-6df3d1f0a281";

        var audiences = ApiAudienceValidation.ResolveValidAudiences(clientId, null);

        CollectionAssert.AreEquivalent(
        new[]
        {
            clientId,
            $"api://{clientId}"
        }, audiences);
    }

    [TestMethod]
    public void ResolveValidAudiences_Adds_Guid_Audience_For_Configured_ApiUri()
    {
        var clientId = "73df32d0-82b8-4eba-bc4a-6df3d1f0a281";
        var configuredAudience = $"api://{clientId}";

        var audiences = ApiAudienceValidation.ResolveValidAudiences(clientId, configuredAudience);

        CollectionAssert.Contains(audiences, clientId);
        CollectionAssert.Contains(audiences, configuredAudience);
    }

    [TestMethod]
    public void ResolveValidAudiences_Preserves_Custom_Audience()
    {
        var clientId = "73df32d0-82b8-4eba-bc4a-6df3d1f0a281";
        const string configuredAudience = "https://fleet-api.contoso.com";

        var audiences = ApiAudienceValidation.ResolveValidAudiences(clientId, configuredAudience);

        CollectionAssert.Contains(audiences, clientId);
        CollectionAssert.Contains(audiences, $"api://{clientId}");
        CollectionAssert.Contains(audiences, configuredAudience);
    }
}
