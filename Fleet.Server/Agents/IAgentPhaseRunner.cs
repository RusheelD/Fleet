namespace Fleet.Server.Agents;

/// <summary>
/// Runs a single agent phase: loads the role prompt, builds the tool set,
/// and executes an LLM tool-calling loop until the agent produces final output.
/// </summary>
public interface IAgentPhaseRunner
{
    /// <summary>
    /// Runs a single phase of the agent pipeline.
    /// </summary>
    /// <param name="role">The agent role to run.</param>
    /// <param name="userMessage">The user-level instruction (work item context + previous phase outputs).</param>
    /// <param name="toolContext">Shared context with sandbox, credentials, etc.</param>
    /// <param name="modelOverride">Optional model override (e.g., opus for complex tasks). Uses GenerateModel if null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The phase result including the final LLM output.</returns>
    Task<PhaseResult> RunPhaseAsync(
        AgentRole role,
        string userMessage,
        AgentToolContext toolContext,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);
}
