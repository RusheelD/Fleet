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
    IWorkItemAttachmentService workItemAttachmentService,
    IProjectImportExportService projectImportExportService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    private const int MaxWorkItemAttachmentBytes = 10 * 1024 * 1024;

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
    public async Task<IActionResult> Delete(string projectId, int workItemNumber, CancellationToken cancellationToken = default)
    {
        var deleted = await workItemService.DeleteAsync(projectId, workItemNumber, cancellationToken);
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

    [HttpGet("{workItemNumber:int}/attachments")]
    public async Task<IActionResult> GetAttachments(string projectId, int workItemNumber, CancellationToken cancellationToken = default)
    {
        var attachments = await workItemAttachmentService.GetByWorkItemNumberAsync(projectId, workItemNumber, cancellationToken);
        return Ok(attachments);
    }

    [HttpPost("{workItemNumber:int}/attachments")]
    [RequestSizeLimit(MaxWorkItemAttachmentBytes)]
    public async Task<IActionResult> UploadAttachment(string projectId, int workItemNumber, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (file.Length > MaxWorkItemAttachmentBytes)
            return BadRequest($"File exceeds the {MaxWorkItemAttachmentBytes / (1024 * 1024)} MB limit.");

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var attachment = await workItemAttachmentService.UploadAsync(
            projectId,
            workItemNumber,
            file.FileName,
            file.ContentType,
            buffer.ToArray(),
            cancellationToken);

        if (attachment is null)
            return NotFound();

        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId, workItemNumber, attachmentId = attachment.Id },
            cancellationToken);

        return Created($"/api/projects/{projectId}/work-items/{workItemNumber}/attachments/{attachment.Id}", attachment);
    }

    [HttpDelete("{workItemNumber:int}/attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(string projectId, int workItemNumber, string attachmentId, CancellationToken cancellationToken = default)
    {
        var deleted = await workItemAttachmentService.DeleteAsync(projectId, workItemNumber, attachmentId, cancellationToken);
        if (!deleted)
            return NotFound();

        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId, workItemNumber, attachmentId },
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{workItemNumber:int}/attachments/{attachmentId}/content")]
    public async Task<IActionResult> GetAttachmentContent(string projectId, int workItemNumber, string attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await workItemAttachmentService.GetContentAsync(projectId, workItemNumber, attachmentId, cancellationToken);
        if (attachment is null)
            return NotFound();

        Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(attachment.FileName)}";
        return File(attachment.Content, attachment.ContentType);
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
