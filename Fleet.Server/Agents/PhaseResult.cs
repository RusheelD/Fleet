namespace Fleet.Server.Agents;

/// <summary>
/// Result of running a single phase in the agent pipeline.
/// </summary>
public record PhaseResult(
    /// <summary>The role that ran this phase.</summary>
    AgentRole Role,

    /// <summary>The final text output from the LLM for this phase.</summary>
    string Output,

    /// <summary>Number of tool calls made during this phase.</summary>
    int ToolCallCount,

    /// <summary>Whether the phase completed successfully.</summary>
    bool Success,

    /// <summary>Error message if the phase failed.</summary>
    string? Error = null,

    /// <summary>Latest estimated completion percent reported during this attempt.</summary>
    int EstimatedCompletionPercent = 0,

    /// <summary>Latest status summary reported during this attempt.</summary>
    string? LastProgressSummary = null
);
