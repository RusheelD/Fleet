using Fleet.Server.Auth;
using Fleet.Server.Notifications;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController(
    INotificationService notificationService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var items = await notificationService.GetRecentAsync(userId, unreadOnly);
        return Ok(items);
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await notificationService.MarkAsReadAsync(userId, notificationId);
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.NotificationsUpdated,
            new { notificationId });
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        await notificationService.MarkAllAsReadAsync(userId);
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.NotificationsUpdated,
            new { allRead = true });
        return NoContent();
    }
}
