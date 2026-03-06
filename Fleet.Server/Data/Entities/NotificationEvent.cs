namespace Fleet.Server.Data.Entities;

public class NotificationEvent
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string? ExecutionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
}
