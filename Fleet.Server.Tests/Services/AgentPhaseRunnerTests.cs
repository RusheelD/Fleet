using Fleet.Server.Agents;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentPhaseRunnerTests
{
    [TestMethod]
    public void GetMaxToolCalls_UsesExpandedAgentCeilings()
    {
        Assert.AreEqual(200, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Manager));
        Assert.AreEqual(250, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Planner));
        Assert.AreEqual(400, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Contracts));
        Assert.AreEqual(500, AgentPhaseRunner.GetMaxToolCalls(AgentRole.Backend));
    }

    [TestMethod]
    public void GetMaxToolLoops_UsesExpandedAgentCeilings()
    {
        Assert.AreEqual(200, AgentPhaseRunner.GetMaxToolLoops(AgentRole.Manager));
        Assert.AreEqual(400, AgentPhaseRunner.GetMaxToolLoops(AgentRole.Consolidation));
        Assert.AreEqual(500, AgentPhaseRunner.GetMaxToolLoops(AgentRole.Frontend));
    }
}
