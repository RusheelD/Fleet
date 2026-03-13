namespace Fleet.Server.Data.Entities;

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;

    // Foreign key
    public string ChatSessionId { get; set; } = string.Empty;
    public ChatSession ChatSession { get; set; } = null!;

    public List<ChatAttachment> Attachments { get; set; } = [];
}
