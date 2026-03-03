namespace Fleet.Server.Data.Entities;

public class ChatAttachment
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string UploadedAt { get; set; } = string.Empty;

    // Foreign key
    public string ChatSessionId { get; set; } = string.Empty;
    public ChatSession ChatSession { get; set; } = null!;
}
