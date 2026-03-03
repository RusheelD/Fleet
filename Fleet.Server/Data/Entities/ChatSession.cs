namespace Fleet.Server.Data.Entities;

public class ChatSession
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;

    // Navigation
    public List<ChatMessage> Messages { get; set; } = [];
    public List<ChatAttachment> Attachments { get; set; } = [];
}
