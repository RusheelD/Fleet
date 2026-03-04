using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/levels")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class WorkItemLevelsController(IWorkItemLevelService workItemLevelService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(string projectId)
    {
        // Ensure default levels exist on first access
        await workItemLevelService.EnsureDefaultLevelsAsync(projectId);
        var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
        return Ok(levels);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(string projectId, int id)
    {
        var level = await workItemLevelService.GetByIdAsync(projectId, id);
        return level is null ? NotFound() : Ok(level);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string projectId, [FromBody] CreateWorkItemLevelRequest request)
    {
        var level = await workItemLevelService.CreateAsync(projectId, request);
        return CreatedAtAction(nameof(GetById), new { projectId, id = level.Id }, level);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(string projectId, int id, [FromBody] UpdateWorkItemLevelRequest request)
    {
        var level = await workItemLevelService.UpdateAsync(projectId, id, request);
        return level is null ? NotFound() : Ok(level);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string projectId, int id)
    {
        var deleted = await workItemLevelService.DeleteAsync(projectId, id);
        return deleted ? NoContent() : NotFound();
    }
}
