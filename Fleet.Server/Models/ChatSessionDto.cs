namespace Fleet.Server.Models;

public record ChatSessionDto(
    string Id,
    string Title,
    string LastMessage,
    string Timestamp,
    bool IsActive,
    bool IsGenerating = false
);
