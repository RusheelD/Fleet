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

    public string BuildSummaryMessage()
    {
        if (!HasOutcome)
            return "No dynamic dispatch candidates were found.";

        var summary = $"Dynamic dispatch: {StartedCount} started, {SkippedCount} skipped, {FailedCount} failed (candidates: {CandidateCount}, accepted: {AcceptedCount}).";
        if (Notes.Count == 0)
            return summary;

        var details = string.Join(" | ", Notes.Take(3));
        return $"{summary} Details: {details}";
    }
}
