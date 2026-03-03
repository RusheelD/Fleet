namespace Fleet.Server.Models;

public record ChatSessionDto(
    string Id,
    string Title,
    string LastMessage,
    string Timestamp,
    bool IsActive
);

public record ChatMessageDto(
    string Id,
    string Role,
    string Content,
    string Timestamp
);

public record SendMessageRequest(string Content);

public record CreateSessionRequest(string Title);

public record ChatDataDto(
    ChatSessionDto[] Sessions,
    ChatMessageDto[] Messages,
    string[] Suggestions
);

/// <summary>Rich response returned when the user sends a message.</summary>
public record SendMessageResponseDto(
    string SessionId,
    ChatMessageDto AssistantMessage,
    ToolEventDto[] ToolEvents,
    string? Error
);

/// <summary>Describes a single tool invocation during the AI response.</summary>
public record ToolEventDto(
    string ToolName,
    string ArgumentsJson,
    string Result
);

/// <summary>Metadata for an uploaded document attached to a chat session.</summary>
public record ChatAttachmentDto(
    string Id,
    string FileName,
    int ContentLength,
    string UploadedAt
);
