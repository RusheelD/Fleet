namespace Fleet.Server.Data.Entities;

/// <summary>
/// Persists the result of a single agent phase within an execution.
/// Used to pass context between sequential pipeline phases.
/// </summary>
public class AgentPhaseResult
{
    public int Id { get; set; }

    /// <summary>The agent role that ran this phase (e.g., "Planner", "Backend").</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The final text output from the LLM for this phase.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Number of tool calls made during this phase.</summary>
    public int ToolCallCount { get; set; }

    /// <summary>Whether the phase completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the phase failed (null on success).</summary>
    public string? Error { get; set; }

    /// <summary>When this phase started (UTC).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>When this phase completed (UTC).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Ordinal position in the pipeline (0-based).</summary>
    public int PhaseOrder { get; set; }

    // Foreign key to execution
    public string ExecutionId { get; set; } = string.Empty;
    public AgentExecution Execution { get; set; } = null!;
}
