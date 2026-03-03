namespace Fleet.Server.Data.Entities;

public class DashboardAgent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public double Progress { get; set; }

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;
}
