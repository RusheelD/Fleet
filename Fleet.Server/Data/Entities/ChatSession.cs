namespace Fleet.Server.Data.Entities;

public class ChatSession
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>True while a generate-work-items request is in-flight for this session.</summary>
    public bool IsGenerating { get; set; }
    public string GenerationState { get; set; } = Models.ChatGenerationStates.Idle;
    public string? GenerationStatus { get; set; }
    public DateTime? GenerationUpdatedAtUtc { get; set; }
    public string RecentActivityJson { get; set; } = "[]";
    public bool IsDynamicIterationEnabled { get; set; }
    public string? DynamicIterationBranch { get; set; }
    public string? DynamicIterationPolicyJson { get; set; }

    // Foreign key (null for global chat sessions)
    public string? ProjectId { get; set; }
    public Project? Project { get; set; }

    // Navigation
    public List<ChatMessage> Messages { get; set; } = [];
    public List<ChatAttachment> Attachments { get; set; } = [];
}
