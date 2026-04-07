namespace Fleet.Server.Data.Entities;

public class MemoryEntry
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public string? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool AlwaysInclude { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public UserProfile? UserProfile { get; set; }
    public Project? Project { get; set; }
}
