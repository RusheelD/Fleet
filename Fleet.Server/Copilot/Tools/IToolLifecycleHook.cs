using Fleet.Server.LLM;

namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Lifecycle hooks that intercept tool execution at key points.
/// Implementations can modify, log, or veto tool calls without
/// changing the tool implementations themselves.
/// Inspired by Claude Code's PreToolUse/PostToolUse hook pattern.
/// </summary>
public interface IToolLifecycleHook
{
    /// <summary>Execution order — lower values run first.</summary>
    int Order => 0;

    /// <summary>
    /// Called before a tool is executed. Return a non-null string to override
    /// the tool result (skipping actual execution). Return null to proceed normally.
    /// </summary>
    Task<string?> BeforeExecuteAsync(ToolHookContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <summary>
    /// Called after a tool has executed successfully. Can modify the result.
    /// Return null to keep the original result unchanged.
    /// </summary>
    Task<string?> AfterExecuteAsync(ToolHookContext context, string result, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

/// <summary>Context passed to lifecycle hooks for each tool invocation.</summary>
public record ToolHookContext(
    /// <summary>Name of the tool being called.</summary>
    string ToolName,
    /// <summary>Raw JSON arguments from the model.</summary>
    string ArgumentsJson,
    /// <summary>Current project ID (null for global scope).</summary>
    string? ProjectId,
    /// <summary>Current user ID.</summary>
    string UserId
);
