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
