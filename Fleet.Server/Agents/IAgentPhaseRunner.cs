namespace Fleet.Server.Agents;

/// <summary>
/// Callback invoked when an agent reports its estimated progress.
/// </summary>
/// <param name="estimatedProgress">Agent's estimated completion as a fraction (0.0 – 1.0).</param>
/// <param name="summary">Brief description of current activity.</param>
public delegate Task PhaseProgressCallback(double estimatedProgress, string summary);

/// <summary>
/// Callback invoked after each tool call to log it as a detailed log entry.
/// </summary>
/// <param name="toolName">Name of the tool that was called.</param>
/// <param name="resultSnippet">A short snippet of the tool result for display.</param>
public delegate Task PhaseToolCallLogger(string toolName, string resultSnippet);

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
    /// <param name="maxTokens">Optional max output tokens. Uses the provider default (16384) if null.</param>
    /// <param name="onProgress">Optional callback invoked after every few tool calls to report live progress.</param>
    /// <param name="onToolCall">Optional callback invoked after each tool call to log detailed entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The phase result including the final LLM output.</returns>
    Task<PhaseResult> RunPhaseAsync(
        AgentRole role,
        string userMessage,
        AgentToolContext toolContext,
        string? modelOverride = null,
        int? maxTokens = null,
        PhaseProgressCallback? onProgress = null,
        PhaseToolCallLogger? onToolCall = null,
        CancellationToken cancellationToken = default);
}
