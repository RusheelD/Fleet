namespace Fleet.Server.Models;

public record ProjectDto(
    string Id,
    string OwnerId,
    string Title,
    string Slug,
    string Description,
    string Repo,
    WorkItemSummaryDto WorkItems,
    AgentSummaryDto Agents,
    string LastActivity
);
