namespace Fleet.Server.Models;

/// <summary>
/// Detailed execution metrics for an agent run, including
/// per-phase token usage, tool call counts, and timing.
/// </summary>
public record AgentExecutionMetrics(
    string ExecutionId,
    string Status,
    double Progress,
    int TotalToolCalls,
    long TotalInputTokens,
    long TotalOutputTokens,
    TimeSpan? TotalDuration,
    IReadOnlyList<PhaseMetrics> Phases
);

/// <summary>Metrics for a single execution phase.</summary>
public record PhaseMetrics(
    string Role,
    int PhaseOrder,
    bool Success,
    int ToolCallCount,
    int InputTokens,
    int OutputTokens,
    TimeSpan? Duration,
    string? Error
);
