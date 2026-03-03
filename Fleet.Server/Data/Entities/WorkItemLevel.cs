namespace Fleet.Server.Data.Entities;

public class WorkItemLevel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public bool IsDefault { get; set; }

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;

    // Navigation — work items at this level
    public List<WorkItem> WorkItems { get; set; } = [];
}
