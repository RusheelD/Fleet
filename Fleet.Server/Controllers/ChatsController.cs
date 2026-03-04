using Fleet.Server.Auth;
using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/chat")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class ChatsController(IChatService chatService) : ControllerBase
{
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
        return Created($"/api/projects/{projectId}/chat/sessions/{session.Id}", session);
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string projectId, string sessionId)
    {
        var deleted = await chatService.DeleteSessionAsync(projectId, sessionId);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(string projectId, string sessionId, [FromBody] SendMessageRequest request)
    {
        var response = await chatService.SendMessageAsync(projectId, sessionId, request.Content, request.GenerateWorkItems);
        return Ok(response);
    }

    // ── Attachments ──────────────────────────────────────────

    [HttpGet("sessions/{sessionId}/attachments")]
    public async Task<IActionResult> GetAttachments(string projectId, string sessionId)
    {
        var attachments = await chatService.GetAttachmentsAsync(sessionId);
        return Ok(attachments);
    }

    [HttpPost("sessions/{sessionId}/attachments")]
    [RequestSizeLimit(1_048_576)] // 1 MB per upload
    public async Task<IActionResult> UploadAttachment(string projectId, string sessionId, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .md (Markdown) files are supported.");

        if (file.Length > 512_000) // 500 KB limit for a single markdown file
            return BadRequest("File exceeds the 500 KB limit.");

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        var attachment = await chatService.UploadAttachmentAsync(projectId, sessionId, file.FileName, content);
        return Created($"/api/projects/{projectId}/chat/sessions/{sessionId}/attachments/{attachment.Id}", attachment);
    }

    [HttpDelete("sessions/{sessionId}/attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(string projectId, string sessionId, string attachmentId)
    {
        var deleted = await chatService.DeleteAttachmentAsync(attachmentId);
        return deleted ? NoContent() : NotFound();
    }
}
