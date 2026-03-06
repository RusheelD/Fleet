using Fleet.Server.Models;

namespace Fleet.Server.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationEventDto>> GetRecentAsync(int userId, bool unreadOnly);
    Task MarkAsReadAsync(int userId, int notificationId);
    Task MarkAllAsReadAsync(int userId);
    Task<NotificationEventDto?> PublishAsync(
        int userId,
        string projectId,
        string type,
        string title,
        string message,
        string? executionId = null);
}
