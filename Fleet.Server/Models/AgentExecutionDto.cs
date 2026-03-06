namespace Fleet.Server.Models;

public record AgentExecutionDto(
    string Id,
    int WorkItemId,
    string WorkItemTitle,
    string Status,
    AgentInfoDto[] Agents,
    string StartedAt,
    string Duration,
    double Progress,
    string? BranchName = null,
    string? PullRequestUrl = null,
    string? CurrentPhase = null
);
