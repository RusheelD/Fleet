namespace Fleet.Server.Data.Entities;

public class AgentExecution
{
    public string Id { get; set; } = string.Empty;
    public int WorkItemId { get; set; }
    public string WorkItemTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public double Progress { get; set; }

    // Stored as JSON (jsonb) — collection of owned entities
    public List<AgentInfo> Agents { get; set; } = [];

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;
}

/// <summary>
/// Owned entity stored as JSON within AgentExecution.
/// Represents a sub-agent within an execution.
/// </summary>
public class AgentInfo
{
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentTask { get; set; } = string.Empty;
    public double Progress { get; set; }
}
