namespace Fleet.Server.Models;

/// <summary>Rich response returned when the user sends a message.</summary>
public record SendMessageResponseDto(
    string SessionId,
    ChatMessageDto? AssistantMessage,
    ToolEventDto[] ToolEvents,
    string? Error,
    bool IsDeferred = false
);
