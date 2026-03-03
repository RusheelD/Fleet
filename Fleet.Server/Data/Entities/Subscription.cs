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

/// <summary>
/// Owned entity stored as JSON within Subscription.
/// </summary>
public class UsageMeterData
{
    public string Label { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Remaining { get; set; } = string.Empty;
}

/// <summary>
/// Owned entity stored as JSON within Subscription.
/// Features list demonstrates nested collections inside JSON.
/// </summary>
public class PlanData
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Features { get; set; } = [];
    public string ButtonLabel { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string ButtonAppearance { get; set; } = string.Empty;
}
