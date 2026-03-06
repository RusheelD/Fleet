namespace Fleet.Server.Models;

public record NotificationEventDto(
    int Id,
    string Type,
    string Title,
    string Message,
    string ProjectId,
    string? ExecutionId,
    bool IsRead,
    DateTime CreatedAtUtc
);
