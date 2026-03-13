namespace Fleet.Server.Models;

public record ChatMessageDto(
    string Id,
    string Role,
    string Content,
    string Timestamp)
{
    public ChatAttachmentDto[] Attachments { get; init; } = [];
}
