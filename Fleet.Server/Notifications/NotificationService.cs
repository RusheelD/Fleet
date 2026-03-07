using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Notifications;

public class NotificationService(
    INotificationRepository notificationRepository,
    FleetDbContext db,
    IServerEventPublisher eventPublisher,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task<IReadOnlyList<NotificationEventDto>> GetRecentAsync(int userId, bool unreadOnly)
        => notificationRepository.GetRecentAsync(userId, unreadOnly);

    public Task MarkAsReadAsync(int userId, int notificationId)
        => notificationRepository.MarkAsReadAsync(userId, notificationId);

    public Task MarkAllAsReadAsync(int userId)
        => notificationRepository.MarkAllAsReadAsync(userId);

    public async Task<NotificationEventDto?> PublishAsync(
        int userId,
        string projectId,
        string type,
        string title,
        string message,
        string? executionId = null)
    {
        var preferences = await db.UserProfiles
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Preferences)
            .FirstOrDefaultAsync();

        if (!IsNotificationEnabled(type, preferences))
        {
            logger.LogDebug(
                "Skipping notification type '{Type}' for user {UserId} due to user preference",
                type, userId);
            return null;
        }

        var created = await notificationRepository.CreateAsync(userId, projectId, type, title, message, executionId);
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.NotificationsUpdated,
            new
            {
                projectId,
                type,
                notificationId = created.Id,
            });

        return created;
    }

    private static bool IsNotificationEnabled(string type, UserPreferences? preferences)
    {
        if (preferences is null)
            return true;

        return type switch
        {
            "execution_completed" => preferences.AgentCompletedNotification,
            "pr_ready" => preferences.PrOpenedNotification,
            "execution_failed" => preferences.AgentErrorsNotification,
            "execution_needs_input" => preferences.WorkItemUpdatesNotification,
            _ => true,
        };
    }
}
