namespace Fleet.Server.Copilot;

public sealed record DynamicIterationDispatchResult(
    int CandidateCount,
    int AcceptedCount,
    int StartedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<string> Notes)
{
    public static readonly DynamicIterationDispatchResult Empty = new(0, 0, 0, 0, 0, []);

    public bool HasOutcome => CandidateCount > 0 || AcceptedCount > 0 || StartedCount > 0 || SkippedCount > 0 || FailedCount > 0;

    public static DynamicIterationDispatchResult FromAutoDispatch(
        int candidateCount,
        AgentAutoExecutionDispatchResult dispatchResult)
    {
        var started = dispatchResult.WorkItems.Count(item => !string.IsNullOrWhiteSpace(item.ExecutionId));
        var failed = dispatchResult.WorkItems.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));
        var skipped = Math.Max(0, candidateCount - started - failed);
        var accepted = dispatchResult.WorkItems.Count(item =>
            string.Equals(item.Status, "started", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "queued", StringComparison.OrdinalIgnoreCase));
        var notes = dispatchResult.WorkItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Reason))
            .Select(item => $"#{item.WorkItemNumber}: {item.Reason}")
            .ToArray();

        return new DynamicIterationDispatchResult(
            candidateCount,
            accepted,
            started,
            skipped,
            failed,
            notes);
    }

    public string BuildSummaryMessage()
    {
        if (!HasOutcome)
            return "No dynamic dispatch candidates were found.";

        var summary = $"Dynamic iteration dispatch: {StartedCount} started, {SkippedCount} skipped, {FailedCount} failed (candidates: {CandidateCount}, accepted: {AcceptedCount}).";
        if (Notes.Count == 0)
            return summary;

        var details = string.Join(" | ", Notes.Take(3));
        return $"{summary} Details: {details}";
    }
}
