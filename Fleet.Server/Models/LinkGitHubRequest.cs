using System.ComponentModel.DataAnnotations;

namespace Fleet.Server.Models;

public record LinkGitHubRequest(
    [param: Required] string Code,
    [param: Required] string RedirectUri,
    [param: Required] string State);
