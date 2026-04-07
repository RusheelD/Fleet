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
    public void ShouldCreatePullRequestForExecution_ReturnsFalse_ForSubFlowExecutions()
    {
        Assert.IsFalse(AgentOrchestrationService.ShouldCreatePullRequestForExecution("parent-execution"));
        Assert.IsTrue(AgentOrchestrationService.ShouldCreatePullRequestForExecution(null));
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

    [TestMethod]
    public void ShouldMaterializeGeneratedSubFlows_ReturnsFalse_WhenExecutionDepthLimitReached()
    {
        var workItem = CreateWorkItem(40, "Deep nested feature", 5);
        var generatedPlan = new GeneratedSubFlowPlan(
            "Still trying to split",
            [
                new GeneratedSubFlowSpec("Backend", "API work", 2, 4, [], "", []),
                new GeneratedSubFlowSpec("Frontend", "UI work", 2, 3, [], "", []),
            ]);

        Assert.IsFalse(AgentOrchestrationService.ShouldMaterializeGeneratedSubFlows(
            workItem,
            generatedPlan,
            AgentOrchestrationService.MaxSubFlowExecutionDepth));
    }

    [TestMethod]
    public void ShouldOrchestrateExistingSubFlows_ReturnsFalse_ForSingleChildChain()
    {
        var parent = CreateWorkItem(50, "Feature shell", 5, childNumbers: [51]);
        var child = CreateWorkItem(51, "Only child", 4, parent: 50, childNumbers: [52]);
        var grandchild = CreateWorkItem(52, "Leaf component", 3, parent: 51);
        var descendants = new[] { child, grandchild };

        Assert.IsFalse(AgentOrchestrationService.ShouldOrchestrateExistingSubFlows(
            parent,
            [child],
            descendants,
            executionDepth: 0));
    }

    [TestMethod]
    public void ShouldOrchestrateExistingSubFlows_ReturnsFalse_ForSimpleFeatureBranches()
    {
        var parent = CreateWorkItem(60, "Small feature", 4, childNumbers: [61, 62]);
        var childA = CreateWorkItem(61, "Button copy tweak", 2, parent: 60);
        var childB = CreateWorkItem(62, "Validation message tweak", 2, parent: 60);
        var descendants = new[] { childA, childB };

        Assert.IsFalse(AgentOrchestrationService.ShouldOrchestrateExistingSubFlows(
            parent,
            descendants,
            descendants,
            executionDepth: 0));
    }

    [TestMethod]
    public void ShouldOrchestrateExistingSubFlows_ReturnsTrue_ForComplexParallelBranches()
    {
        var parent = CreateWorkItem(70, "Large sync feature", 5, childNumbers: [71, 72]);
        var childA = CreateWorkItem(71, "Server sync engine", 4, parent: 70, childNumbers: [73]);
        var childB = CreateWorkItem(72, "Client sync UI", 3, parent: 70);
        var grandchild = CreateWorkItem(73, "Conflict resolution", 3, parent: 71);
        var descendants = new[] { childA, childB, grandchild };

        Assert.IsTrue(AgentOrchestrationService.ShouldOrchestrateExistingSubFlows(
            parent,
            new[] { childA, childB },
            descendants,
            executionDepth: 0));
    }

    [TestMethod]
    public void ShouldOrchestrateExistingSubFlows_ReturnsTrue_ForHighDifficultyNestedParallelBranches()
    {
        var parent = CreateWorkItem(74, "Cross-platform workspace revamp", 5, childNumbers: [75, 76, 77]);
        var childA = CreateWorkItem(75, "Desktop shell rewrite", 5, parent: 74, childNumbers: [78, 79]);
        var childB = CreateWorkItem(76, "Mobile workspace parity", 4, parent: 74, childNumbers: [80]);
        var childC = CreateWorkItem(77, "Release docs", 2, parent: 74);
        var grandchildA1 = CreateWorkItem(78, "Window management", 4, parent: 75);
        var grandchildA2 = CreateWorkItem(79, "Navigation state", 3, parent: 75);
        var grandchildB1 = CreateWorkItem(80, "Responsive execution cards", 3, parent: 76);
        var descendants = new[] { childA, childB, childC, grandchildA1, grandchildA2, grandchildB1 };

        Assert.IsTrue(AgentOrchestrationService.ShouldOrchestrateExistingSubFlows(
            parent,
            new[] { childA, childB, childC },
            descendants,
            executionDepth: 0));
    }

    [TestMethod]
    public void ShouldOrchestrateExistingSubFlows_ReturnsFalse_WhenExecutionDepthLimitReached()
    {
        var parent = CreateWorkItem(80, "Deep parent feature", 5, childNumbers: [81, 82]);
        var childA = CreateWorkItem(81, "Backend branch", 4, parent: 80);
        var childB = CreateWorkItem(82, "Frontend branch", 4, parent: 80);
        var descendants = new[] { childA, childB };

        Assert.IsFalse(AgentOrchestrationService.ShouldOrchestrateExistingSubFlows(
            parent,
            descendants,
            descendants,
            AgentOrchestrationService.MaxSubFlowExecutionDepth));
    }

    [TestMethod]
    public void ShouldMaterializeGeneratedSubFlows_ReturnsTrue_ForHighDifficultyNestedParallelPlan()
    {
        var workItem = CreateWorkItem(90, "Platform-wide sync overhaul", 5);
        var generatedPlan = new GeneratedSubFlowPlan(
            "Split the platform work into meaningful parallel branches",
            [
                new GeneratedSubFlowSpec(
                    "Desktop sync engine",
                    "Rebuild the desktop sync shell and orchestration.",
                    1,
                    5,
                    [],
                    "",
                    [
                        new GeneratedSubFlowSpec("Window lifecycle", "Handle shell lifecycle.", 2, 4, [], "", []),
                        new GeneratedSubFlowSpec("Navigation persistence", "Preserve navigation state.", 2, 3, [], "", []),
                    ]),
                new GeneratedSubFlowSpec(
                    "Mobile sync experience",
                    "Bring mobile workflow parity.",
                    1,
                    4,
                    [],
                    "",
                    [
                        new GeneratedSubFlowSpec("Responsive run cards", "Support the compact UI.", 2, 3, [], "", []),
                    ]),
                new GeneratedSubFlowSpec("Release notes", "Document the rollout.", 3, 2, [], "", []),
            ]);

        Assert.IsTrue(AgentOrchestrationService.ShouldMaterializeGeneratedSubFlows(
            workItem,
            generatedPlan,
            executionDepth: 0));
    }

    [TestMethod]
    public void ShouldMaterializeGeneratedSubFlows_ReturnsTrue_ForThreeParallelLeafBranchesEvenWhenParentIsD4()
    {
        var workItem = CreateWorkItem(91, "Chess game feature", 4);
        var generatedPlan = new GeneratedSubFlowPlan(
            "Split engine, AI, and UI work into separate branches",
            [
                new GeneratedSubFlowSpec(
                    "Engine core",
                    "Implement state, move generation, and legality.",
                    1,
                    5,
                    ["engine"],
                    "Engine tests pass.",
                    []),
                new GeneratedSubFlowSpec(
                    "AI engine",
                    "Implement evaluation and search.",
                    1,
                    4,
                    ["ai"],
                    "AI returns legal moves.",
                    []),
                new GeneratedSubFlowSpec(
                    "UI + Hosting + Docs",
                    "Implement UI and deployment docs.",
                    2,
                    4,
                    ["ui"],
                    "UI works and docs are updated.",
                    []),
            ]);

        Assert.IsTrue(AgentOrchestrationService.ShouldMaterializeGeneratedSubFlows(
            workItem,
            generatedPlan,
            executionDepth: 0));
    }

    private static Models.WorkItemDto CreateWorkItem(
        int number,
        string title,
        int difficulty,
        int? parent = null,
        params int[] childNumbers)
        => new(
            WorkItemNumber: number,
            Title: title,
            State: "New",
            Priority: 2,
            Difficulty: difficulty,
            AssignedTo: "Fleet AI",
            Tags: [],
            IsAI: true,
            Description: title,
            ParentWorkItemNumber: parent,
            ChildWorkItemNumbers: childNumbers,
            LevelId: null);
}
