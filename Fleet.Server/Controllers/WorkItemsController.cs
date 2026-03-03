using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/work-items")]
public class WorkItemsController(IWorkItemService workItemService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(string projectId)
    {
        var items = await workItemService.GetByProjectIdAsync(projectId);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(string projectId, int id)
    {
        var item = await workItemService.GetByIdAsync(projectId, id);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string projectId, [FromBody] CreateWorkItemRequest request)
    {
        var item = await workItemService.CreateAsync(projectId, request);
        return CreatedAtAction(nameof(GetById), new { projectId, id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(string projectId, int id, [FromBody] UpdateWorkItemRequest request)
    {
        var item = await workItemService.UpdateAsync(projectId, id, request);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string projectId, int id)
    {
        var deleted = await workItemService.DeleteAsync(projectId, id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
