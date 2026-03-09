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
    IAuthService authService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>GET /api/connections — List all linked accounts for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetConnections()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var connections = await connectionService.GetConnectionsAsync(userId);
        return Ok(connections);
    }

    /// <summary>GET /api/connections/github/state — Creates a signed OAuth state value for GitHub auth.</summary>
    [HttpGet("github/state")]
    public async Task<IActionResult> GetGitHubOAuthState()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var state = await connectionService.CreateGitHubOAuthStateAsync(userId);
        return Ok(state);
    }

    /// <summary>GET /api/connections/github/client-id — Returns the GitHub OAuth client ID configured on the server.</summary>
    [HttpGet("github/client-id")]
    public IActionResult GetGitHubClientId()
    {
        var clientId = configuration["GitHub:ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return NotFound(new { message = "GitHub OAuth client ID is not configured." });
        }

        return Ok(new { clientId });
    }

    /// <summary>POST /api/connections/github — Exchange a GitHub OAuth code and link the account.</summary>
    [HttpPost("github")]
    public async Task<IActionResult> LinkGitHub([FromBody] LinkGitHubRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var result = await connectionService.LinkGitHubAsync(userId, request.Code, request.RedirectUri, request.State);
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

    /// <summary>DELETE /api/connections/github/{accountId} — Unlink a specific GitHub account.</summary>
    [HttpDelete("github/{accountId:int}")]
    public async Task<IActionResult> UnlinkGitHubById(int accountId)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await connectionService.UnlinkGitHubAsync(userId, accountId);
        return NoContent();
    }

    [HttpPut("github/{accountId:int}/primary")]
    public async Task<IActionResult> SetPrimaryGitHubAccount(int accountId)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var account = await connectionService.SetPrimaryGitHubAccountAsync(userId, accountId);
        return Ok(account);
    }

    /// <summary>GET /api/connections/github/repos — List the user's GitHub repositories.</summary>
    [HttpGet("github/repos")]
    public async Task<IActionResult> GetGitHubRepos([FromQuery] int? accountId = null)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var repos = await connectionService.GetGitHubRepositoriesAsync(userId, accountId);
        return Ok(repos);
    }

    [HttpPost("github/repos")]
    public async Task<IActionResult> CreateGitHubRepo([FromBody] CreateGitHubRepositoryRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var repo = await connectionService.CreateGitHubRepositoryAsync(userId, request);
        return Ok(repo);
    }
}
