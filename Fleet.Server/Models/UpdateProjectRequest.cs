namespace Fleet.Server.Models;

public record UpdateProjectRequest(
    string? Title,
    string? Description,
    string? Repo,
    string? BranchPattern = null,
    string? CommitAuthorMode = null,
    string? CommitAuthorName = null,
    string? CommitAuthorEmail = null);
