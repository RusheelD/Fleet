namespace Fleet.Server.Models;

public record ChatDataDto(
    ChatSessionDto[] Sessions,
    ChatMessageDto[] Messages,
    string[] Suggestions
);
