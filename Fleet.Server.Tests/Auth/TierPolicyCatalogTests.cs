using Fleet.Server.Auth;

namespace Fleet.Server.Tests.Auth;

[TestClass]
public class TierPolicyCatalogTests
{
    [TestMethod]
    public void FreeTier_TemporarilyMatchesUnlimitedAccess()
    {
        var policy = TierPolicyCatalog.Get(UserRoles.Free);

        Assert.IsNull(policy.MonthlyWorkItemRuns);
        Assert.IsNull(policy.MonthlyCodingRuns);
        Assert.IsTrue(policy.UnlimitedRateLimit);
        Assert.AreEqual(int.MaxValue, policy.MaxConcurrentAgentsPerTask);
        Assert.AreEqual(int.MaxValue, policy.MaxActiveAgentExecutions);
    }

    [TestMethod]
    public void UnlimitedTier_IsUncapped()
    {
        var policy = TierPolicyCatalog.Get(UserRoles.Unlimited);

        Assert.IsNull(policy.MonthlyWorkItemRuns);
        Assert.IsNull(policy.MonthlyCodingRuns);
        Assert.IsTrue(policy.UnlimitedRateLimit);
        Assert.AreEqual(int.MaxValue, policy.MaxConcurrentAgentsPerTask);
        Assert.AreEqual(int.MaxValue, policy.MaxActiveAgentExecutions);
    }
}
