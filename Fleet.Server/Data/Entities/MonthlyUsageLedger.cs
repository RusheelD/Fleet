namespace Fleet.Server.Data.Entities;

public class MonthlyUsageLedger
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public string UtcMonth { get; set; } = string.Empty; // yyyy-MM
    public int WorkItemRunCharges { get; set; }
    public int WorkItemRunRefunds { get; set; }
    public int CodingRunCharges { get; set; }
    public int CodingRunRefunds { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CachedInputTokens { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
