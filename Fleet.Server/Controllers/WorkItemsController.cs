using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.Realtime;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/work-items")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class WorkItemsController(
    IWorkItemService workItemService,
    IProjectImportExportService projectImportExportService,
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

    [HttpGet("export")]
    public async Task<IActionResult> Export(string projectId, CancellationToken cancellationToken)
    {
        var payload = await projectImportExportService.ExportWorkItemsAsync(projectId, cancellationToken);
        if (payload is null) return NotFound();

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"fleet-work-items-{projectId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(bytes, "application/json", fileName);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(
        string projectId,
        [FromBody] ProjectWorkItemsExportFileDto payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var importResult = await projectImportExportService.ImportWorkItemsAsync(projectId, payload, cancellationToken);
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.WorkItemsUpdated,
                new { projectId, importedCount = importResult.WorkItemsImported },
                cancellationToken);
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId },
                cancellationToken);
            return Ok(importResult);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid import file",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext?.Request?.Path.ToString() ?? $"/api/projects/{projectId}/work-items/import",
            });
        }
    }
}
