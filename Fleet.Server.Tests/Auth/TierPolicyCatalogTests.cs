using Fleet.Server.Auth;

namespace Fleet.Server.Tests.Auth;

[TestClass]
public class TierPolicyCatalogTests
{
    [TestMethod]
    public void FreeTier_HasExpectedMvpLimits()
    {
        var policy = TierPolicyCatalog.Get(UserRoles.Free);

        Assert.AreEqual(4, policy.MonthlyWorkItemRuns);
        Assert.AreEqual(2, policy.MonthlyCodingRuns);
        Assert.AreEqual(120, policy.RequestsPerMinute);
        Assert.AreEqual(1, policy.MaxConcurrentAgentsPerTask);
        Assert.AreEqual(1, policy.MaxActiveAgentExecutions);
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
