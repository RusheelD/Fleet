using Fleet.Server.Agents;
using Fleet.Server.Data.Entities;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ReviewFeedbackLoopPlannerTests
{
    private static readonly AgentRole[][] FullPipeline =
    [
        [AgentRole.Manager],
        [AgentRole.Planner],
        [AgentRole.Contracts],
        [AgentRole.Backend, AgentRole.Frontend, AgentRole.Testing, AgentRole.Styling],
        [AgentRole.Consolidation],
        [AgentRole.Review, AgentRole.Documentation],
    ];

    [TestMethod]
    public void ParseDecision_ReadsStructuredJsonManifest()
    {
        var output = """
            ## Review Summary

            Recommendation: PATCH

            ```json
            {
              "recommendation": "PATCH",
              "highest_severity": "P1",
              "summary": "A localized backend bug remains.",
              "rationale": "The change is close, but one request path still fails.",
              "target_roles": ["Backend", "Testing"],
              "restart_from": null,
              "findings": [
                {
                  "severity": "P1",
                  "role": "Backend",
                  "description": "Empty branch names are still accepted.",
                  "suggestion": "Reject empty values and add a regression test."
                }
              ]
            }
            ```
            """;

        var decision = ReviewFeedbackLoopPlanner.ParseDecision(output);

        Assert.AreEqual(ReviewTriageRecommendation.Patch, decision.Recommendation);
        Assert.AreEqual("P1", decision.HighestSeverity);
        CollectionAssert.AreEqual(
            new[] { AgentRole.Backend, AgentRole.Testing },
            decision.TargetRoles.ToArray());
        Assert.AreEqual(1, decision.Findings.Count);
    }

    [TestMethod]
    public void DetermineRolesToRerun_ForPatch_IncludesValidationAndReviewTail()
    {
        var decision = new ReviewTriageDecision(
            ReviewTriageRecommendation.Patch,
            "P1",
            "Backend validation bug remains.",
            "A localized backend fix is still needed.",
            [],
            [AgentRole.Backend],
            null);

        var rerunRoles = ReviewFeedbackLoopPlanner.DetermineRolesToRerun(FullPipeline, decision);

        CollectionAssert.AreEqual(
            new[]
            {
                AgentRole.Backend,
                AgentRole.Testing,
                AgentRole.Consolidation,
                AgentRole.Review,
                AgentRole.Documentation,
            },
            rerunRoles.ToArray());
    }

    [TestMethod]
    public void DetermineRolesToRerun_ForRestart_ReentersFromRequestedPhase()
    {
        var decision = new ReviewTriageDecision(
            ReviewTriageRecommendation.Restart,
            "P0",
            "Contracts are wrong.",
            "The implementation is built on the wrong interface shape.",
            [],
            [],
            AgentRole.Contracts);

        var rerunRoles = ReviewFeedbackLoopPlanner.DetermineRolesToRerun(FullPipeline, decision);

        CollectionAssert.AreEqual(
            new[]
            {
                AgentRole.Contracts,
                AgentRole.Backend,
                AgentRole.Frontend,
                AgentRole.Testing,
                AgentRole.Styling,
                AgentRole.Consolidation,
                AgentRole.Review,
                AgentRole.Documentation,
            },
            rerunRoles.ToArray());
    }

    [TestMethod]
    public void SummarizeExecutionReviews_CountsAutomaticLoops()
    {
        var phaseResults = new List<AgentPhaseResult>
        {
            new()
            {
                ExecutionId = "exec-1",
                Role = AgentRole.Review.ToString(),
                Success = true,
                PhaseOrder = 5,
                Output = """
                    ```json
                    {
                      "recommendation": "PATCH",
                      "highest_severity": "P1",
                      "summary": "One fix remains.",
                      "rationale": "Localized issue.",
                      "target_roles": ["Backend"],
                      "restart_from": null,
                      "findings": []
                    }
                    ```
                    """
            },
            new()
            {
                ExecutionId = "exec-1",
                Role = AgentRole.Review.ToString(),
                Success = true,
                PhaseOrder = 8,
                Output = """
                    ```json
                    {
                      "recommendation": "STOP",
                      "highest_severity": "P3",
                      "summary": "Ready to ship.",
                      "rationale": "Only nits remain.",
                      "target_roles": [],
                      "restart_from": null,
                      "findings": []
                    }
                    ```
                    """
            },
        };

        var summary = ReviewFeedbackLoopPlanner.SummarizeExecutionReviews(phaseResults);

        Assert.AreEqual(1, summary.AutomaticLoopCount);
        Assert.AreEqual("STOP", summary.LastRecommendation);
    }
}
