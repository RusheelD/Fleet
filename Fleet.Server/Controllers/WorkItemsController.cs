using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/work-items")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class WorkItemsController(IWorkItemService workItemService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(string projectId)
    {
        var items = await workItemService.GetByProjectIdAsync(projectId);
        return Ok(items);
    }

    [HttpGet("{workItemNumber:int}")]
    public async Task<IActionResult> GetByWorkItemNumber(string projectId, int workItemNumber)
    {
        var item = await workItemService.GetByWorkItemNumberAsync(projectId, workItemNumber);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string projectId, [FromBody] CreateWorkItemRequest request)
    {
        var item = await workItemService.CreateAsync(projectId, request);
        return CreatedAtAction(nameof(GetByWorkItemNumber), new { projectId, workItemNumber = item.WorkItemNumber }, item);
    }

    [HttpPut("{workItemNumber:int}")]
    public async Task<IActionResult> Update(string projectId, int workItemNumber, [FromBody] UpdateWorkItemRequest request)
    {
        var item = await workItemService.UpdateAsync(projectId, workItemNumber, request);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpDelete("{workItemNumber:int}")]
    public async Task<IActionResult> Delete(string projectId, int workItemNumber)
    {
        var deleted = await workItemService.DeleteAsync(projectId, workItemNumber);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
