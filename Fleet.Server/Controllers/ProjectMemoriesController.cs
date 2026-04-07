using Fleet.Server.Auth;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/memories")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class ProjectMemoriesController(IMemoryService memoryService, IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMemories(string projectId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var memories = await memoryService.GetProjectMemoriesAsync(userId, projectId, cancellationToken);
        return Ok(memories);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMemory(string projectId, [FromBody] UpsertMemoryEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var created = await memoryService.CreateProjectMemoryAsync(userId, projectId, request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("{memoryId:int}")]
    public async Task<IActionResult> UpdateMemory(string projectId, int memoryId, [FromBody] UpsertMemoryEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var updated = await memoryService.UpdateProjectMemoryAsync(userId, projectId, memoryId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{memoryId:int}")]
    public async Task<IActionResult> DeleteMemory(string projectId, int memoryId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await memoryService.DeleteProjectMemoryAsync(userId, projectId, memoryId, cancellationToken);
        return NoContent();
    }
}
