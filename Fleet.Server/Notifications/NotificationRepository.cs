using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Notifications;

public class NotificationRepository(FleetDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationEventDto>> GetRecentAsync(int userId, bool unreadOnly, int limit = 50)
    {
        var query = db.NotificationEvents
            .AsNoTracking()
            .Where(n => n.UserProfileId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task MarkAsReadAsync(int userId, int notificationId)
    {
        var entity = await db.NotificationEvents
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserProfileId == userId);
        if (entity is null)
            return;

        entity.IsRead = true;
        await db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await db.NotificationEvents
            .Where(n => n.UserProfileId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));
    }

    public async Task<NotificationEventDto> CreateAsync(
        int userId,
        string projectId,
        string type,
        string title,
        string message,
        string? executionId = null)
    {
        var entity = new NotificationEvent
        {
            UserProfileId = userId,
            ProjectId = projectId,
            ExecutionId = executionId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.NotificationEvents.Add(entity);
        await db.SaveChangesAsync();
        return Map(entity);
    }

    private static NotificationEventDto Map(NotificationEvent entity) => new(
        entity.Id,
        entity.Type,
        entity.Title,
        entity.Message,
        entity.ProjectId,
        entity.ExecutionId,
        entity.IsRead,
        entity.CreatedAtUtc
    );
}
