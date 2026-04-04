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

    [TestMethod]
    public void ApplyAssignedAgentLimit_RetainsManagerPlannerAndCapsWorkerRoles()
    {
        AgentRole[][] pipeline =
        [
            [AgentRole.Manager],
            [AgentRole.Planner],
            [AgentRole.Contracts],
            [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing],
            [AgentRole.Review],
        ];

        var limited = AgentOrchestrationService.ApplyAssignedAgentLimit(pipeline, "manual", 2);

        Assert.AreEqual(3, limited.Length);
        CollectionAssert.AreEqual(new[] { AgentRole.Manager }, limited[0]);
        CollectionAssert.AreEqual(new[] { AgentRole.Planner }, limited[1]);
        CollectionAssert.AreEqual(new[] { AgentRole.Backend, AgentRole.Frontend }, limited[2]);
    }

    [TestMethod]
    public void ResolveMaxConcurrentAgentsPerTask_ClampsTierLimitToAssignedAgentCount()
    {
        Assert.AreEqual(2, AgentOrchestrationService.ResolveMaxConcurrentAgentsPerTask(4, "manual", 2));
        Assert.AreEqual(1, AgentOrchestrationService.ResolveMaxConcurrentAgentsPerTask(4, "manual", 1));
        Assert.AreEqual(4, AgentOrchestrationService.ResolveMaxConcurrentAgentsPerTask(4, "manual", null));
    }

    [TestMethod]
    public void ApplyAssignedAgentLimit_DoesNotCapAutoAssignedWorkItems()
    {
        AgentRole[][] pipeline =
        [
            [AgentRole.Manager],
            [AgentRole.Planner],
            [AgentRole.Contracts],
            [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing],
            [AgentRole.Review],
        ];

        var unlimited = AgentOrchestrationService.ApplyAssignedAgentLimit(pipeline, "auto", 2);

        Assert.AreEqual(pipeline.Length, unlimited.Length);
        CollectionAssert.AreEqual(pipeline.SelectMany(group => group).ToArray(), unlimited.SelectMany(group => group).ToArray());
    }

    [TestMethod]
    public void ResolveDefaultPipeline_UsesFullPipelineForStandardRuns()
    {
        var pipeline = AgentOrchestrationService.ResolveDefaultPipeline(AgentExecutionModes.Standard);

        Assert.AreEqual(6, pipeline.Length);
        CollectionAssert.AreEqual(new[] { AgentRole.Manager }, pipeline[0]);
        CollectionAssert.AreEqual(new[] { AgentRole.Planner }, pipeline[1]);
        CollectionAssert.AreEqual(new[] { AgentRole.Contracts }, pipeline[2]);
        CollectionAssert.AreEqual(new[] { AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling }, pipeline[3]);
        CollectionAssert.AreEqual(new[] { AgentRole.Consolidation }, pipeline[4]);
        CollectionAssert.AreEqual(new[] { AgentRole.Review, AgentRole.Documentation }, pipeline[5]);
    }

    [TestMethod]
    public void ResolveDefaultPipeline_UsesPreludeForOrchestrationRuns()
    {
        var pipeline = AgentOrchestrationService.ResolveDefaultPipeline(AgentExecutionModes.Orchestration);

        Assert.AreEqual(2, pipeline.Length);
        CollectionAssert.AreEqual(new[] { AgentRole.Manager }, pipeline[0]);
        CollectionAssert.AreEqual(new[] { AgentRole.Planner }, pipeline[1]);
    }

    [TestMethod]
    public void ResolveMaxConcurrentAgentsPerTask_DoesNotClampAutoAssignedWorkItems()
    {
        Assert.AreEqual(4, AgentOrchestrationService.ResolveMaxConcurrentAgentsPerTask(4, "auto", 1));
        Assert.AreEqual(4, AgentOrchestrationService.ResolveMaxConcurrentAgentsPerTask(4, "auto", 5));
    }

    [TestMethod]
    public void ShouldMaterializeGeneratedSubFlows_ReturnsFalse_ForSmallNestedTask()
    {
        var workItem = new Models.WorkItemDto(
            WorkItemNumber: 12,
            Title: "Tight leaf task",
            State: "New",
            Priority: 2,
            Difficulty: 3,
            AssignedTo: "Fleet AI",
            Tags: [],
            IsAI: true,
            Description: "Small enough to finish directly.",
            ParentWorkItemNumber: 5,
            ChildWorkItemNumbers: [],
            LevelId: null);

        var generatedPlan = new GeneratedSubFlowPlan(
            "Split it further",
            [
                new GeneratedSubFlowSpec("Child A", "Child", 2, 2, [], "", []),
                new GeneratedSubFlowSpec("Child B", "Child", 2, 2, [], "", []),
            ]);

        Assert.IsFalse(AgentOrchestrationService.ShouldMaterializeGeneratedSubFlows(workItem, generatedPlan));
    }

    [TestMethod]
    public void ShouldMaterializeGeneratedSubFlows_ReturnsFalse_WhenSingleChildJustPuntsWork()
    {
        var workItem = new Models.WorkItemDto(
            WorkItemNumber: 21,
            Title: "Parent task",
            State: "New",
            Priority: 2,
            Difficulty: 4,
            AssignedTo: "Fleet AI",
            Tags: [],
            IsAI: true,
            Description: "A substantial task.",
            ParentWorkItemNumber: null,
            ChildWorkItemNumbers: [],
            LevelId: null);

        var generatedPlan = new GeneratedSubFlowPlan(
            "Split it once",
            [
                new GeneratedSubFlowSpec("Same task again", "No real breakdown", 2, 4, [], "", []),
            ]);

        Assert.IsFalse(AgentOrchestrationService.ShouldMaterializeGeneratedSubFlows(workItem, generatedPlan));
    }

    [TestMethod]
    public void ShouldMaterializeGeneratedSubFlows_ReturnsTrue_ForMeaningfulTopLevelBreakdown()
    {
        var workItem = new Models.WorkItemDto(
            WorkItemNumber: 34,
            Title: "Large feature",
            State: "New",
            Priority: 1,
            Difficulty: 5,
            AssignedTo: "Fleet AI",
            Tags: [],
            IsAI: true,
            Description: "Big enough to split.",
            ParentWorkItemNumber: null,
            ChildWorkItemNumbers: [],
            LevelId: null);

        var generatedPlan = new GeneratedSubFlowPlan(
            "Separate backend and frontend",
            [
                new GeneratedSubFlowSpec("Backend", "API work", 2, 4, [], "", []),
                new GeneratedSubFlowSpec("Frontend", "UI work", 2, 3, [], "", []),
            ]);

        Assert.IsTrue(AgentOrchestrationService.ShouldMaterializeGeneratedSubFlows(workItem, generatedPlan));
    }
}
