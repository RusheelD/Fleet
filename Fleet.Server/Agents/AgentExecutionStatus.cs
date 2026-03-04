namespace Fleet.Server.Agents;

/// <summary>
/// Lightweight status snapshot for polling agent executions.
/// </summary>
public record AgentExecutionStatus(
    string ExecutionId,
    string Status,
    string? CurrentPhase,
    double Progress,
    string? BranchName,
    string? PullRequestUrl,
    string? Error
);
