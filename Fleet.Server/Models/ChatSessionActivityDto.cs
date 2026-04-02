namespace Fleet.Server.Models;

public record ChatSessionActivityDto(
    string Id,
    string Kind,
    string Message,
    string TimestampUtc,
    string? ToolName = null,
    bool? Succeeded = null
);
