namespace Fleet.Server.Data.Entities;

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
