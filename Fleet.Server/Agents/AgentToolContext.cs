namespace Fleet.Server.Agents;

/// <summary>
/// Context passed to every agent tool invocation during a phase.
/// </summary>
public record AgentToolContext(
    /// <summary>The repo sandbox for this execution.</summary>
    IRepoSandbox Sandbox,

    /// <summary>The project ID being worked on.</summary>
    string ProjectId,

    /// <summary>The user ID who owns the execution.</summary>
    string UserId,

    /// <summary>The GitHub access token for API calls.</summary>
    string AccessToken,

    /// <summary>The repo full name (owner/repo).</summary>
    string RepoFullName,

    /// <summary>The execution ID for logging.</summary>
    string ExecutionId
)
{
    /// <summary>
    /// Shared scratchpad for inter-phase communication within an execution.
    /// Agents can write named entries that downstream phases read selectively.
    /// </summary>
    public AgentScratchpad Scratchpad { get; init; } = new();
}
