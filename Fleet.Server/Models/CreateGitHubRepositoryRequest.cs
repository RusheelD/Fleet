using System.ComponentModel.DataAnnotations;

namespace Fleet.Server.Models;

public record CreateGitHubRepositoryRequest(
    [param: Required] string Name,
    string? Description = null,
    bool Private = false,
    int? AccountId = null
);
