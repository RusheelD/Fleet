using System.Text.Json;
using Fleet.Server.Notifications;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists user notifications with optional unread filter.</summary>
public class ListNotificationsTool(INotificationService notificationService) : IChatTool
{
    public string Name => "list_notifications";

    public string Description =>
        "List notification events for the current user. Optionally return only unread items.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "unreadOnly": {
                    "type": "boolean",
                    "description": "If true, return only unread notifications."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of notifications to return (default 50)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(context.UserId, out var userId))
            return "Error: invalid user ID.";

        var args = ParseArgs(argumentsJson);
        var notifications = await notificationService.GetRecentAsync(userId, args.UnreadOnly);
        var result = notifications
            .Take(args.Limit)
            .Select(notification => new
            {
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.ProjectId,
                notification.ExecutionId,
                notification.IsRead,
                notification.CreatedAtUtc,
            })
            .ToList();

        return result.Count == 0
            ? "No notifications found."
            : JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ListNotificationsArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var unreadOnly = root.TryGetProperty("unreadOnly", out var unreadOnlyEl) &&
                             unreadOnlyEl.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                             unreadOnlyEl.GetBoolean();
            var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var limitValue)
                ? limitValue
                : 50;
            return new ListNotificationsArgs(unreadOnly, Math.Clamp(limit, 1, 200));
        }
        catch
        {
            return new ListNotificationsArgs(false, 50);
        }
    }

    private sealed record ListNotificationsArgs(bool UnreadOnly, int Limit);
}
