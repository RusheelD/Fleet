namespace Fleet.Server.Data.Entities;

public class Subscription
{
    public int Id { get; set; }
    public string CurrentPlanName { get; set; } = string.Empty;
    public string CurrentPlanDescription { get; set; } = string.Empty;

    // Stored as JSON (jsonb) — collections of complex objects
    public List<UsageMeterData> UsageMeters { get; set; } = [];
    public List<PlanData> Plans { get; set; } = [];
}
