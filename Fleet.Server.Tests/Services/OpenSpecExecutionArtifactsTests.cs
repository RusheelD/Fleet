using Fleet.Server.Agents;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class OpenSpecExecutionArtifactsTests
{
    [TestMethod]
    public void BuildPaths_UsesOpenSpecChangeAndSpecStructure()
    {
        var workItem = CreateWorkItem();

        var paths = OpenSpecExecutionArtifacts.BuildPaths("fleet/42-add-auth-copy", workItem);

        Assert.AreEqual("fleet-42-add-auth-copy", paths.ChangeId);
        Assert.AreEqual("work-item-42-add-auth", paths.CapabilityId);
        Assert.AreEqual(".fleet/.docs/changes/fleet-42-add-auth-copy/proposal.md", paths.ProposalPath);
        Assert.AreEqual(".fleet/.docs/changes/fleet-42-add-auth-copy/tasks.md", paths.TasksPath);
        Assert.AreEqual(".fleet/.docs/changes/fleet-42-add-auth-copy/design.md", paths.DesignPath);
        Assert.AreEqual(
            ".fleet/.docs/changes/fleet-42-add-auth-copy/specs/work-item-42-add-auth/spec.md",
            paths.SpecPath);
    }

    [TestMethod]
    public void BuildSnapshot_IncludesTrackedFilesAndPromptSummary()
    {
        var execution = CreateExecution();
        var workItem = CreateWorkItem();
        var phaseResults = new List<AgentPhaseResult>
        {
            new() { ExecutionId = execution.Id, Role = "Manager", Success = true, Output = "Setup complete", PhaseOrder = 0 },
            new() { ExecutionId = execution.Id, Role = "Planner", Success = true, Output = "Plan created", PhaseOrder = 1 },
            new() { ExecutionId = execution.Id, Role = "Backend", Success = false, Output = "Started API work", Error = "tests failing", PhaseOrder = 2 },
        };
        var descendants = new List<AgentExecution>
        {
            new()
            {
                Id = "child-1",
                WorkItemId = 43,
                WorkItemTitle = "Docs",
                Status = "completed",
                StartedAt = "2026-04-17T00:00:00Z",
                ProjectId = execution.ProjectId,
                UserId = execution.UserId,
                BranchName = "fleet/43-docs",
            },
        };

        var snapshot = OpenSpecExecutionArtifacts.BuildSnapshot(
            execution,
            workItem,
            "main",
            phaseResults,
            descendants,
            "# Execution Journal\n\nBackend work started.");

        CollectionAssert.AreEqual(
            new[]
            {
                snapshot.Paths.ProposalPath,
                snapshot.Paths.TasksPath,
                snapshot.Paths.DesignPath,
                snapshot.Paths.SpecPath,
            },
            snapshot.TrackedPaths.ToArray());
        StringAssert.Contains(snapshot.TasksMarkdown, "Manager");
        StringAssert.Contains(snapshot.TasksMarkdown, "#43 Docs (`completed`)");
        StringAssert.Contains(snapshot.DesignMarkdown, "# Execution Journal");
        StringAssert.Contains(snapshot.SpecMarkdown, "ADDED Requirements");
        StringAssert.Contains(snapshot.PromptContext, snapshot.Paths.ProposalPath);
        StringAssert.Contains(snapshot.PromptContext, "Completed phases so far: Manager, Planner.");
        StringAssert.Contains(snapshot.PromptContext, "Failed phases recorded so far: Backend.");
        StringAssert.Contains(snapshot.PromptContext, "Sub-flow status: 1 completed");
    }

    private static WorkItemDto CreateWorkItem()
        => new(
            WorkItemNumber: 42,
            Title: "Add Auth",
            State: "In Progress (AI)",
            Priority: 2,
            Difficulty: 4,
            AssignedTo: "Fleet AI",
            Tags: ["backend", "auth"],
            IsAI: true,
            Description: "Implement authentication.",
            ParentWorkItemNumber: null,
            ChildWorkItemNumbers: [],
            LevelId: null,
            AcceptanceCriteria: "- Users can sign in\n- Sessions persist");

    private static AgentExecution CreateExecution()
        => new()
        {
            Id = "exec-42",
            WorkItemId = 42,
            WorkItemTitle = "Add Auth",
            Status = "running",
            StartedAt = "2026-04-17T00:00:00Z",
            ProjectId = "proj-1",
            UserId = "7",
            BranchName = "fleet/42-add-auth-copy",
            CurrentPhase = "Backend",
            Agents =
            [
                new AgentInfo { Role = "Manager", Status = "completed", CurrentTask = "Done", Progress = 1.0 },
                new AgentInfo { Role = "Planner", Status = "completed", CurrentTask = "Done", Progress = 1.0 },
                new AgentInfo { Role = "Backend", Status = "running", CurrentTask = "Implementing API", Progress = 0.4 },
                new AgentInfo { Role = "Review", Status = "idle", CurrentTask = "Waiting", Progress = 0.0 },
            ],
        };
}
