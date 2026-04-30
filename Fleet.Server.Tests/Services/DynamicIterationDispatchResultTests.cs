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
}
