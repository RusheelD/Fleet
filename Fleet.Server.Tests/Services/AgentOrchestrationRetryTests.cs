using Fleet.Server.Agents;
using Fleet.Server.Data.Entities;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentOrchestrationRetryTests
{
    [TestMethod]
    public void BuildRetryCarryForwardOutputs_UsesLatestSuccessfulOutputsForKnownRoles()
    {
        var priorPhaseResults = new List<AgentPhaseResult>
        {
            new() { Role = "Manager", Success = true, Output = "Initial manager output", PhaseOrder = 0 },
            new() { Role = "Planner", Success = false, Output = "Failed planner output", PhaseOrder = 1 },
            new() { Role = "Planner", Success = true, Output = "Recovered planner output", PhaseOrder = 2 },
            new() { Role = "UnknownRole", Success = true, Output = "Should be ignored", PhaseOrder = 3 },
        };

        var carryForwardOutputs = AgentOrchestrationService.BuildRetryCarryForwardOutputs(priorPhaseResults);

        Assert.AreEqual(2, carryForwardOutputs.Count);
        Assert.AreEqual("Initial manager output", carryForwardOutputs[AgentRole.Manager]);
        Assert.AreEqual("Recovered planner output", carryForwardOutputs[AgentRole.Planner]);
    }

    [TestMethod]
    public void BuildAgentInfoList_MarksCarriedRolesAsCompleted()
    {
        AgentRole[][] pipeline =
        [
            [AgentRole.Manager],
            [AgentRole.Planner],
            [AgentRole.Backend, AgentRole.Testing],
        ];

        var carryForwardOutputs = new Dictionary<AgentRole, string>
        {
            [AgentRole.Manager] = "manager",
            [AgentRole.Backend] = "backend",
        };

        var agents = AgentOrchestrationService.BuildAgentInfoList(pipeline, carryForwardOutputs);

        var manager = agents.Single(agent => agent.Role == AgentRole.Manager.ToString());
        var planner = agents.Single(agent => agent.Role == AgentRole.Planner.ToString());
        var backend = agents.Single(agent => agent.Role == AgentRole.Backend.ToString());

        Assert.AreEqual("completed", manager.Status);
        Assert.AreEqual(1.0, manager.Progress);
        Assert.AreEqual("idle", planner.Status);
        Assert.AreEqual("completed", backend.Status);
    }

    [TestMethod]
    public void BuildCarryForwardPhaseOutputs_RespectsPipelineOrder()
    {
        AgentRole[][] pipeline =
        [
            [AgentRole.Manager],
            [AgentRole.Planner],
            [AgentRole.Backend, AgentRole.Frontend],
        ];

        var carryForwardOutputs = new Dictionary<AgentRole, string>
        {
            [AgentRole.Backend] = "backend",
            [AgentRole.Manager] = "manager",
        };

        var outputs = AgentOrchestrationService.BuildCarryForwardPhaseOutputs(pipeline, carryForwardOutputs);

        CollectionAssert.AreEqual(
            new[] { AgentRole.Manager, AgentRole.Backend },
            outputs.Select(output => output.Role).ToArray());
    }

    [TestMethod]
    public void BuildResumeCarryForwardOutputs_UsesOnlyRolesStillMarkedCompleted()
    {
        var priorPhaseResults = new List<AgentPhaseResult>
        {
            new() { Role = "Manager", Success = true, Output = "manager", PhaseOrder = 0 },
            new() { Role = "Planner", Success = true, Output = "planner", PhaseOrder = 1 },
            new() { Role = "Backend", Success = true, Output = "backend-before-review-loop", PhaseOrder = 2 },
        };

        var persistedAgents = new List<AgentInfo>
        {
            new() { Role = "Manager", Status = "completed", CurrentTask = "Done", Progress = 1.0 },
            new() { Role = "Planner", Status = "completed", CurrentTask = "Done", Progress = 1.0 },
            new() { Role = "Backend", Status = "idle", CurrentTask = "Queued from review PATCH", Progress = 0 },
        };

        var carryForwardOutputs = AgentOrchestrationService.BuildResumeCarryForwardOutputs(priorPhaseResults, persistedAgents);

        Assert.AreEqual(2, carryForwardOutputs.Count);
        Assert.IsTrue(carryForwardOutputs.ContainsKey(AgentRole.Manager));
        Assert.IsTrue(carryForwardOutputs.ContainsKey(AgentRole.Planner));
        Assert.IsFalse(carryForwardOutputs.ContainsKey(AgentRole.Backend));
    }

    [TestMethod]
    public void BuildPipelineFromExecutionAgents_ReconstructsStructuredPipeline()
    {
        var persistedAgents = new List<AgentInfo>
        {
            new() { Role = "Manager", Status = "completed", CurrentTask = "Done", Progress = 1.0 },
            new() { Role = "Planner", Status = "completed", CurrentTask = "Done", Progress = 1.0 },
            new() { Role = "Backend", Status = "running", CurrentTask = "Implementing", Progress = 0.4 },
            new() { Role = "Testing", Status = "idle", CurrentTask = "Waiting", Progress = 0 },
            new() { Role = "Review", Status = "idle", CurrentTask = "Waiting", Progress = 0 },
        };

        var pipeline = AgentOrchestrationService.BuildPipelineFromExecutionAgents(persistedAgents);

        Assert.AreEqual(4, pipeline.Length);
        CollectionAssert.AreEqual(new[] { AgentRole.Manager }, pipeline[0]);
        CollectionAssert.AreEqual(new[] { AgentRole.Planner }, pipeline[1]);
        CollectionAssert.AreEqual(new[] { AgentRole.Backend, AgentRole.Testing }, pipeline[2]);
        CollectionAssert.AreEqual(new[] { AgentRole.Review }, pipeline[3]);
    }
}
