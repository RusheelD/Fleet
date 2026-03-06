using Fleet.Server.Models;

namespace Fleet.Server.Notifications;

public interface INotificationRepository
{
    Task<IReadOnlyList<NotificationEventDto>> GetRecentAsync(int userId, bool unreadOnly, int limit = 50);
    Task MarkAsReadAsync(int userId, int notificationId);
    Task MarkAllAsReadAsync(int userId);
    Task<NotificationEventDto> CreateAsync(
        int userId,
        string projectId,
        string type,
        string title,
        string message,
        string? executionId = null);
}
