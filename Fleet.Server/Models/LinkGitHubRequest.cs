using System.ComponentModel.DataAnnotations;

namespace Fleet.Server.Models;

public record LinkGitHubRequest(
    [property: Required] string Code,
    [property: Required] string RedirectUri,
    [property: Required] string State);
