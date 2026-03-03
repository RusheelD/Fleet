using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/chat")]
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

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(string projectId, string sessionId, [FromBody] SendMessageRequest request)
    {
        var message = await chatService.SendMessageAsync(projectId, sessionId, request.Content);
        return Created($"/api/projects/{projectId}/chat/sessions/{sessionId}/messages/{message.Id}", message);
    }
}
