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

    /// <summary>The branch created for this execution (e.g., "fleet/42-add-auth").</summary>
    public string? BranchName { get; set; }

    /// <summary>URL of the pull request created by the agents, if any.</summary>
    public string? PullRequestUrl { get; set; }

    /// <summary>The current phase being executed (e.g., "Planner", "Backend").</summary>
    public string? CurrentPhase { get; set; }

    /// <summary>When this execution actually started (UTC).</summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>When this execution completed (UTC).</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>The user ID who triggered this execution.</summary>
    public string UserId { get; set; } = string.Empty;

    // Stored as JSON (jsonb) — collection of owned entities
    public List<AgentInfo> Agents { get; set; } = [];

    // Navigation: phase results persisted as rows
    public List<AgentPhaseResult> PhaseResults { get; set; } = [];

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;
}
