using Fleet.Server.Auth;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/events")]
public class EventsController(
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    [HttpGet("stream")]
    public async Task Stream([FromQuery] string? projectId, CancellationToken cancellationToken)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.SubscribeAsync(userId, projectId, Response, cancellationToken);
    }
}
