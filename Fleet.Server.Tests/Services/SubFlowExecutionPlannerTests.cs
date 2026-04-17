using Fleet.Server.Agents;
using Fleet.Server.Models;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class SubFlowExecutionPlannerTests
{
    [TestMethod]
    public void Resolve_UsesExistingChildDependencyPlanToStageSubFlows()
    {
        var children = new[]
        {
            CreateWorkItem(40, "Core implementation"),
            CreateWorkItem(41, "Testing hardening"),
            CreateWorkItem(42, "Documentation wrap-up"),
        };

        const string plannerOutput = """
            EXECUTION_PLAN_JSON
            {
              "effective_difficulty": 5,
              "difficulty_reason": "Cross-stack release work.",
              "subflow_mode": "use_existing_subflows",
              "subflow_reason": "Use the existing child work items.",
              "following_agent_count": 2,
              "following_agents": ["Contracts", "Consolidation"],
              "existing_subflow_dependencies": [
                {
                  "work_item_number": 42,
                  "depends_on_work_item_numbers": [40, 41],
                  "reason": "Docs should wait for the implementation branches."
                }
              ]
            }

            SUBFLOW_PLAN_JSON
            { "split": false }
            """;

        var plan = SubFlowExecutionPlanner.Resolve(children, plannerOutput);

        Assert.AreEqual(2, plan.Batches.Count);
        CollectionAssert.AreEqual(new[] { 40, 41 }, plan.Batches[0].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 42 }, plan.Batches[1].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 40, 41 }, plan.DependenciesByWorkItemNumber[42].ToArray());
    }

    [TestMethod]
    public void Resolve_UsesGeneratedSiblingDependenciesToStageSubFlows()
    {
        var children = new[]
        {
            CreateWorkItem(50, "Core Game Logic"),
            CreateWorkItem(51, "Rendering & UI"),
            CreateWorkItem(52, "Documentation"),
        };

        const string plannerOutput = """
            EXECUTION_PLAN_JSON
            {
              "effective_difficulty": 5,
              "difficulty_reason": "Split into distinct branches.",
              "subflow_mode": "generate_subflows",
              "subflow_reason": "Generate new child work items.",
              "following_agent_count": 2,
              "following_agents": ["Contracts", "Consolidation"]
            }

            SUBFLOW_PLAN_JSON
            {
              "split": true,
              "reason": "Split implementation from docs.",
              "subflows": [
                {
                  "title": "Core Game Logic",
                  "description": "Build the game loop.",
                  "priority": 3,
                  "difficulty": 4,
                  "tags": ["backend"],
                  "acceptance_criteria": "Game loop exists.",
                  "subflows": []
                },
                {
                  "title": "Rendering & UI",
                  "description": "Build rendering and UI.",
                  "priority": 3,
                  "difficulty": 4,
                  "tags": ["frontend"],
                  "acceptance_criteria": "UI exists.",
                  "subflows": []
                },
                {
                  "title": "Documentation",
                  "description": "Document the completed game.",
                  "priority": 2,
                  "difficulty": 2,
                  "tags": ["docs"],
                  "acceptance_criteria": "Docs exist.",
                  "depends_on": ["Core Game Logic", "Rendering & UI"],
                  "subflows": []
                }
              ]
            }
            """;

        var plan = SubFlowExecutionPlanner.Resolve(children, plannerOutput);

        Assert.AreEqual(2, plan.Batches.Count);
        CollectionAssert.AreEqual(new[] { 50, 51 }, plan.Batches[0].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 52 }, plan.Batches[1].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 50, 51 }, plan.DependenciesByWorkItemNumber[52].ToArray());
    }

    [TestMethod]
    public void Resolve_FallsBackToDefaultOrderWhenDependencyPlanCycles()
    {
        var children = new[]
        {
            CreateWorkItem(60, "Core implementation"),
            CreateWorkItem(61, "Documentation"),
        };

        const string plannerOutput = """
            EXECUTION_PLAN_JSON
            {
              "effective_difficulty": 4,
              "difficulty_reason": "Still orchestrated.",
              "subflow_mode": "use_existing_subflows",
              "subflow_reason": "Use existing child work items.",
              "following_agent_count": 2,
              "following_agents": ["Contracts", "Consolidation"],
              "existing_subflow_dependencies": [
                {
                  "work_item_number": 60,
                  "depends_on_work_item_numbers": [61],
                  "reason": "Bad cycle setup."
                },
                {
                  "work_item_number": 61,
                  "depends_on_work_item_numbers": [60],
                  "reason": "Bad cycle setup."
                }
              ]
            }

            SUBFLOW_PLAN_JSON
            { "split": false }
            """;

        var plan = SubFlowExecutionPlanner.Resolve(children, plannerOutput);

        Assert.AreEqual(1, plan.Batches.Count);
        CollectionAssert.AreEqual(new[] { 60, 61 }, plan.Batches[0].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        Assert.IsFalse(string.IsNullOrWhiteSpace(plan.WarningMessage));
        StringAssert.Contains(plan.WarningMessage, "cycle");
    }

    [TestMethod]
    public void Resolve_AutomaticallyDefersGitHubPagesDeploymentUntilOtherSubFlowsFinish()
    {
        var children = new[]
        {
            CreateWorkItem(70, "Core app implementation"),
            CreateWorkItem(71, "UI polish"),
            CreateWorkItem(72, "Deployment (GitHub Pages)", description: "Deploy and host the app on GitHub Pages."),
        };

        const string plannerOutput = """
            EXECUTION_PLAN_JSON
            {
              "effective_difficulty": 5,
              "difficulty_reason": "Cross-stack app with deployment.",
              "subflow_mode": "use_existing_subflows",
              "subflow_reason": "Use the existing child work items.",
              "following_agent_count": 2,
              "following_agents": ["Contracts", "Consolidation"]
            }

            SUBFLOW_PLAN_JSON
            { "split": false }
            """;

        var plan = SubFlowExecutionPlanner.Resolve(children, plannerOutput);

        Assert.AreEqual(2, plan.Batches.Count);
        CollectionAssert.AreEqual(new[] { 70, 71 }, plan.Batches[0].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 72 }, plan.Batches[1].WorkItems.Select(item => item.WorkItemNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 70, 71 }, plan.DependenciesByWorkItemNumber[72].ToArray());
    }

    private static WorkItemDto CreateWorkItem(int number, string title, string description = "")
        => new(
            number,
            title,
            "New",
            3,
            3,
            string.Empty,
            [],
            true,
            description,
            null,
            [],
            null);
}
