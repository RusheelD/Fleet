namespace Fleet.Server.Data.Entities;

/// <summary>
/// Owned entity stored as JSON within AgentExecution representing a sub-agent.
/// </summary>
public class AgentInfo
{
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentTask { get; set; } = string.Empty;
    public double Progress { get; set; }
}
