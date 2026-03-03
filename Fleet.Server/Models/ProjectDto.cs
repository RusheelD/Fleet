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

public record WorkItemSummaryDto(int Total, int Active, int Resolved);

public record AgentSummaryDto(int Total, int Running);

public record CreateProjectRequest(string Title, string Description, string Repo);

public record UpdateProjectRequest(string? Title, string? Description, string? Repo);

public record SlugCheckResult(string Slug, bool Available);
