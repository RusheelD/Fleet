using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/chat")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class ChatsController(
    IChatService chatService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    private const int MaxChatAttachmentBytes = 10 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> GetChatData(string projectId)
    {
        var data = await chatService.GetChatDataAsync(projectId);
        return Ok(data);
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(string projectId, string sessionId)
    {
        var messages = await chatService.GetMessagesAsync(projectId, sessionId);
        return Ok(messages);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(string projectId, [FromBody] CreateSessionRequest request)
    {
        var session = await chatService.CreateSessionAsync(projectId, request.Title);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.ChatUpdated,
            new { projectId, sessionId = session.Id });
        return Created($"/api/projects/{projectId}/chat/sessions/{session.Id}", session);
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string projectId, string sessionId)
    {
        var deleted = await chatService.DeleteSessionAsync(projectId, sessionId);
        if (deleted)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatUpdated,
                new { projectId, sessionId });
        }
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{sessionId}/cancel-generation")]
    public async Task<IActionResult> CancelGeneration(string projectId, string sessionId)
    {
        var canceled = await chatService.CancelGenerationAsync(projectId, sessionId);
        if (canceled)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatUpdated,
                new { projectId, sessionId });
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.WorkItemsUpdated,
                new { projectId, sessionId });
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId });
        }

        return canceled ? NoContent() : NotFound();
    }

    [HttpPut("sessions/{sessionId}")]
    public async Task<IActionResult> RenameSession(string projectId, string sessionId, [FromBody] RenameSessionRequest request)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Session title is required.");

        var renamed = await chatService.RenameSessionAsync(projectId, sessionId, title);
        if (renamed)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatUpdated,
                new { projectId, sessionId });
        }
        return renamed ? NoContent() : NotFound();
    }

    [HttpPut("sessions/{sessionId}/dynamic-iteration")]
    public async Task<IActionResult> UpdateSessionDynamicIteration(
        string projectId,
        string sessionId,
        [FromBody] UpdateSessionDynamicIterationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.DynamicIterationPolicyJson))
        {
            try
            {
                JsonDocument.Parse(request.DynamicIterationPolicyJson);
            }
            catch (JsonException)
            {
                return BadRequest("Dynamic iteration policy must be valid JSON.");
            }
        }

        bool updated;
        try
        {
            updated = await chatService.UpdateSessionDynamicIterationAsync(
                projectId,
                sessionId,
                request.IsDynamicIterationEnabled,
                request.DynamicIterationBranch,
                request.DynamicIterationPolicyJson);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext?.Request?.Path.ToString() ?? $"/api/projects/{projectId}/chat",
            });
        }

        if (updated)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatUpdated,
                new { projectId, sessionId });
        }

        return updated ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(
        string projectId,
        string sessionId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return BadRequest(CreateProjectScopeProblemDetails(
                "Project scope is required.",
                "A project-scoped chat request must include a valid project id."));
        }

        if (!request.GenerateWorkItems && request.DynamicIteration is not null)
        {
            return BadRequest(CreateProjectScopeProblemDetails(
                "Dynamic iteration overrides require work-item generation.",
                "Set generateWorkItems to true when providing dynamicIteration options."));
        }

        SendMessageResponseDto response;
        try
        {
            var sendOptions = new ChatSendOptions(request.GenerateWorkItems, request.DynamicIteration);
            response = await chatService.SendMessageAsync(
                projectId,
                sessionId,
                request.Content,
                sendOptions,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new EmptyResult();
        }

        var userId = await authService.GetCurrentUserIdAsync();
        if (response.IsDeferred)
        {
            return Accepted(response);
        }

        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.ChatUpdated,
            new { projectId, sessionId });

        if (request.GenerateWorkItems)
        {
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.WorkItemsUpdated,
                new { projectId, sessionId });
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId });
        }

        return Ok(response);
    }

    private ProblemDetails CreateProjectScopeProblemDetails(string title, string detail)
        => new()
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext?.Request?.Path.ToString() ?? "/api/projects/{projectId}/chat",
        };

    // ── Attachments ──────────────────────────────────────────

    [HttpGet("sessions/{sessionId}/attachments")]
    public async Task<IActionResult> GetAttachments(string projectId, string sessionId)
    {
        var attachments = await chatService.GetAttachmentsAsync(projectId, sessionId);
        return Ok(attachments);
    }

    [HttpPost("sessions/{sessionId}/attachments")]
    [RequestSizeLimit(MaxChatAttachmentBytes)]
    public async Task<IActionResult> UploadAttachment(string projectId, string sessionId, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (file.Length > MaxChatAttachmentBytes)
            return BadRequest($"File exceeds the {MaxChatAttachmentBytes / (1024 * 1024)} MB limit.");

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        var attachment = await chatService.UploadAttachmentAsync(
            projectId,
            sessionId,
            file.FileName,
            file.ContentType,
            buffer.ToArray(),
            HttpContext?.RequestAborted ?? CancellationToken.None);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.ChatUpdated,
            new { projectId, sessionId, attachmentId = attachment.Id });
        return Created($"/api/projects/{projectId}/chat/sessions/{sessionId}/attachments/{attachment.Id}", attachment);
    }

    [HttpDelete("sessions/{sessionId}/attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(string projectId, string sessionId, string attachmentId)
    {
        var deleted = await chatService.DeleteAttachmentAsync(projectId, sessionId, attachmentId);
        if (deleted)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishProjectEventAsync(
                userId,
                projectId,
                ServerEventTopics.ChatUpdated,
                new { projectId, sessionId, attachmentId });
        }
        return deleted ? NoContent() : NotFound();
    }
}
