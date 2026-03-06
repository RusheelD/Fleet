namespace Fleet.Server.Models;

public record ExecutionDocumentationDto(
    string ExecutionId,
    string Title,
    string Markdown,
    string? PullRequestUrl,
    string? DiffUrl
);
