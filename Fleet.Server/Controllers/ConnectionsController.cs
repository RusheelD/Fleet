using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConnectionsController(
    IConnectionService connectionService,
    IAuthService authService) : ControllerBase
{
    /// <summary>GET /api/connections — List all linked accounts for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetConnections()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var connections = await connectionService.GetConnectionsAsync(userId);
        return Ok(connections);
    }

    /// <summary>POST /api/connections/github — Exchange a GitHub OAuth code and link the account.</summary>
    [HttpPost("github")]
    public async Task<IActionResult> LinkGitHub([FromBody] LinkGitHubRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var result = await connectionService.LinkGitHubAsync(userId, request.Code, request.RedirectUri);
        return Ok(result);
    }

    /// <summary>DELETE /api/connections/github — Unlink the GitHub account.</summary>
    [HttpDelete("github")]
    public async Task<IActionResult> UnlinkGitHub()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await connectionService.UnlinkGitHubAsync(userId);
        return NoContent();
    }

    /// <summary>GET /api/connections/github/repos — List the user's GitHub repositories.</summary>
    [HttpGet("github/repos")]
    public async Task<IActionResult> GetGitHubRepos()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var repos = await connectionService.GetGitHubRepositoriesAsync(userId);
        return Ok(repos);
    }
}
