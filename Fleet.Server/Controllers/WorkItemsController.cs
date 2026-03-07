using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/work-items")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class WorkItemsController(
    IWorkItemService workItemService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
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
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId, workItemNumber = item.WorkItemNumber });
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ProjectsUpdated,
            new { projectId });
        return CreatedAtAction(nameof(GetByWorkItemNumber), new { projectId, workItemNumber = item.WorkItemNumber }, item);
    }

    [HttpPut("{workItemNumber:int}")]
    public async Task<IActionResult> Update(string projectId, int workItemNumber, [FromBody] UpdateWorkItemRequest request)
    {
        var item = await workItemService.UpdateAsync(projectId, workItemNumber, request);
        if (item is null) return NotFound();

        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId, workItemNumber });
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ProjectsUpdated,
            new { projectId });
        return Ok(item);
    }

    [HttpDelete("{workItemNumber:int}")]
    public async Task<IActionResult> Delete(string projectId, int workItemNumber)
    {
        var deleted = await workItemService.DeleteAsync(projectId, workItemNumber);
        if (!deleted) return NotFound();

        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId, workItemNumber });
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ProjectsUpdated,
            new { projectId });
        return NoContent();
    }
}
