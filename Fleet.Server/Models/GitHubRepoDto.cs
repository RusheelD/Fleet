namespace Fleet.Server.Models;

public record GitHubRepoDto(
    string FullName,
    string Name,
    string Owner,
    string? Description,
    bool Private,
    string HtmlUrl
);
