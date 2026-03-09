using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/chat")]
public class GlobalChatsController(
    IChatService chatService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    private const string GlobalProjectScope = "";

    [HttpGet]
    public async Task<IActionResult> GetChatData()
    {
        var data = await chatService.GetChatDataAsync(GlobalProjectScope);
        return Ok(data);
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(string sessionId)
    {
        var messages = await chatService.GetMessagesAsync(GlobalProjectScope, sessionId);
        return Ok(messages);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var session = await chatService.CreateSessionAsync(GlobalProjectScope, request.Title);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ChatUpdated,
            new { projectId = (string?)null, sessionId = session.Id });
        return Created($"/api/chat/sessions/{session.Id}", session);
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        var deleted = await chatService.DeleteSessionAsync(GlobalProjectScope, sessionId);
        if (deleted)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ChatUpdated,
                new { projectId = (string?)null, sessionId });
        }
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("sessions/{sessionId}")]
    public async Task<IActionResult> RenameSession(string sessionId, [FromBody] RenameSessionRequest request)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Session title is required.");

        var renamed = await chatService.RenameSessionAsync(GlobalProjectScope, sessionId, title);
        if (renamed)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ChatUpdated,
                new { projectId = (string?)null, sessionId });
        }
        return renamed ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(string sessionId, [FromBody] SendMessageRequest request)
    {
        if (request.GenerateWorkItems)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Work-item generation is only available when a project is open.",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext?.Request?.Path.ToString() ?? "/api/chat",
            });
        }

        var response = await chatService.SendMessageAsync(GlobalProjectScope, sessionId, request.Content, false);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ChatUpdated,
            new { projectId = (string?)null, sessionId });

        return Ok(response);
    }

    [HttpGet("sessions/{sessionId}/attachments")]
    public async Task<IActionResult> GetAttachments(string sessionId)
    {
        var attachments = await chatService.GetAttachmentsAsync(GlobalProjectScope, sessionId);
        return Ok(attachments);
    }

    [HttpPost("sessions/{sessionId}/attachments")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> UploadAttachment(string sessionId, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .md (Markdown) files are supported.");

        if (file.Length > 512_000)
            return BadRequest("File exceeds the 500 KB limit.");

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        var attachment = await chatService.UploadAttachmentAsync(GlobalProjectScope, sessionId, file.FileName, content);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ChatUpdated,
            new { projectId = (string?)null, sessionId, attachmentId = attachment.Id });
        return Created($"/api/chat/sessions/{sessionId}/attachments/{attachment.Id}", attachment);
    }

    [HttpDelete("sessions/{sessionId}/attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(string sessionId, string attachmentId)
    {
        var deleted = await chatService.DeleteAttachmentAsync(GlobalProjectScope, sessionId, attachmentId);
        if (deleted)
        {
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ChatUpdated,
                new { projectId = (string?)null, sessionId, attachmentId });
        }
        return deleted ? NoContent() : NotFound();
    }
}
