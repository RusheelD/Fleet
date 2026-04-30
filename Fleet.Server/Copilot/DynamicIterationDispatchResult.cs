namespace Fleet.Server.Copilot;

public sealed record DynamicIterationDispatchResult(
    int CandidateCount,
    int AcceptedCount,
    int StartedCount,
    int CoveredCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<string> Notes)
{
    public static readonly DynamicIterationDispatchResult Empty = new(0, 0, 0, 0, 0, 0, []);

    public bool HasOutcome => CandidateCount > 0 || AcceptedCount > 0 || StartedCount > 0 || CoveredCount > 0 || SkippedCount > 0 || FailedCount > 0;

    public static DynamicIterationDispatchResult FromAutoDispatch(
        int candidateCount,
        AgentAutoExecutionDispatchResult dispatchResult)
    {
        var started = dispatchResult.WorkItems.Count(item => !string.IsNullOrWhiteSpace(item.ExecutionId));
        var covered = dispatchResult.WorkItems.Count(item => string.Equals(item.Status, "covered", StringComparison.OrdinalIgnoreCase));
        var failed = dispatchResult.WorkItems.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));
        var skipped = dispatchResult.WorkItems.Count(item => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase)) +
                      Math.Max(0, candidateCount - dispatchResult.WorkItems.Count);
        var accepted = dispatchResult.WorkItems.Count(item =>
            string.Equals(item.Status, "started", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "queued", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "covered", StringComparison.OrdinalIgnoreCase));
        var notes = dispatchResult.WorkItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Reason))
            .Select(item => $"#{item.WorkItemNumber}: {item.Reason}")
            .ToArray();

        return new DynamicIterationDispatchResult(
            candidateCount,
            accepted,
            started,
            covered,
            skipped,
            failed,
            notes);
    }

    public string BuildSummaryMessage()
    {
        if (!HasOutcome)
            return "No dynamic dispatch candidates were found.";

        var summary = $"Dynamic iteration dispatch: {StartedCount} started, {CoveredCount} covered, {SkippedCount} skipped, {FailedCount} failed (candidates: {CandidateCount}, accepted: {AcceptedCount}).";
        if (Notes.Count == 0)
            return summary;

        var details = string.Join(" | ", Notes);
        return $"{summary} Details: {details}";
    }
}
