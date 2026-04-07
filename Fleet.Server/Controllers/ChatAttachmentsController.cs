using Fleet.Server.Copilot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/chat/attachments")]
public class ChatAttachmentsController(IChatService chatService) : ControllerBase
{
    [HttpGet("{attachmentId}/content")]
    public async Task<IActionResult> GetContent(string attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await chatService.GetAttachmentContentAsync(attachmentId, cancellationToken);
        if (attachment is null)
            return NotFound();

        Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(attachment.FileName)}";
        return File(attachment.Content, attachment.ContentType);
    }
}
