using Fleet.Server.Copilot;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class DynamicIterationDispatchResultTests
{
    [TestMethod]
    public void BuildSummaryMessage_IncludesSkippedReasonAfterStartedNotes()
    {
        var result = new DynamicIterationDispatchResult(
            CandidateCount: 4,
            AcceptedCount: 3,
            StartedCount: 3,
            CoveredCount: 0,
            SkippedCount: 1,
            FailedCount: 0,
            Notes:
            [
                "#160: Started automatically.",
                "#161: Started automatically.",
                "#162: Started automatically.",
                "#163: Skipped by policy: message auto-start limit reached (3).",
            ]);

        var summary = result.BuildSummaryMessage();

        StringAssert.Contains(summary, "#160: Started automatically.");
        StringAssert.Contains(summary, "#163: Skipped by policy: message auto-start limit reached (3).");
    }

    [TestMethod]
    public void BuildSummaryMessage_IncludesCoveredCount()
    {
        var result = DynamicIterationDispatchResult.FromAutoDispatch(
            3,
            new AgentAutoExecutionDispatchResult(
                ["exec-160"],
                [
                    new AgentAutoExecutionWorkItemResult(160, "started", "Started automatically.", "exec-160"),
                    new AgentAutoExecutionWorkItemResult(161, "covered", "Covered by parent work item #160; it will run as part of that parent execution."),
                    new AgentAutoExecutionWorkItemResult(162, "covered", "Covered by parent work item #160; it will run as part of that parent execution."),
                ]));

        var summary = result.BuildSummaryMessage();

        Assert.AreEqual(3, result.AcceptedCount);
        Assert.AreEqual(2, result.CoveredCount);
        Assert.AreEqual(0, result.SkippedCount);
        StringAssert.Contains(summary, "1 started, 2 covered, 0 skipped, 0 failed");
    }

    [TestMethod]
    public void FromAutoDispatch_CapsAcceptedCountWhenExecutionRootWasPromoted()
    {
        var result = DynamicIterationDispatchResult.FromAutoDispatch(
            2,
            new AgentAutoExecutionDispatchResult(
                ["exec-160"],
                [
                    new AgentAutoExecutionWorkItemResult(160, "started", "Started automatically.", "exec-160"),
                    new AgentAutoExecutionWorkItemResult(161, "covered", "Covered by parent work item #160; it will run as part of that parent execution."),
                    new AgentAutoExecutionWorkItemResult(162, "covered", "Covered by parent work item #160; it will run as part of that parent execution."),
                ]));

        Assert.AreEqual(2, result.CandidateCount);
        Assert.AreEqual(2, result.AcceptedCount);
        Assert.AreEqual(1, result.StartedCount);
        Assert.AreEqual(2, result.CoveredCount);
    }
}
