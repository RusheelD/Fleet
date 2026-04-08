using Fleet.Server.Auth;
using Fleet.Server.Mcp;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/mcp-servers")]
public class McpServersController(
    IMcpServerService serverService,
    IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetServers()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var servers = await serverService.GetServersAsync(userId);
        return Ok(servers);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await serverService.GetBuiltInTemplatesAsync();
        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> CreateServer([FromBody] UpsertMcpServerRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var server = await serverService.CreateAsync(userId, request);
        return Ok(server);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateServer(int id, [FromBody] UpsertMcpServerRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var server = await serverService.UpdateAsync(userId, id, request);
        return Ok(server);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteServer(int id)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await serverService.DeleteAsync(userId, id);
        return NoContent();
    }

    [HttpPost("{id:int}/validate")]
    public async Task<IActionResult> ValidateServer(int id, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var result = await serverService.ValidateAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("system")]
    public IActionResult GetSystemServers()
    {
        var configs = serverService.GetSystemRuntimeConfigs();
        var result = configs.Select(c => new
        {
            c.Name,
            c.TransportType,
            c.Command,
            c.Arguments,
            c.WorkingDirectory,
            c.Endpoint,
            isSystem = true,
        });
        return Ok(result);
    }
}
