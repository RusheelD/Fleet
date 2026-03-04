namespace Fleet.Server.Data.Entities;

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
