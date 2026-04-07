namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Built-in lifecycle hook that logs tool calls for auditing.
/// Also enforces a hard limit on tool output size to prevent
/// context window overflow.
/// </summary>
public class AuditToolHook(ILogger<AuditToolHook> logger) : IToolLifecycleHook
{
    /// <summary>Max safe output length before truncation.</summary>
    private const int MaxSafeOutputLength = 50_000;

    public int Order => 1000; // Run last

    public Task<string?> AfterExecuteAsync(
        ToolHookContext context, string result, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Tool executed. tool={ToolName} project={ProjectId} user={UserId} resultLength={Length}",
            context.ToolName, context.ProjectId, context.UserId, result.Length);

        // Safety truncation for extremely large outputs
        if (result.Length > MaxSafeOutputLength)
        {
            logger.LogWarning(
                "Tool {ToolName} output truncated from {Original} to {Max} characters",
                context.ToolName, result.Length, MaxSafeOutputLength);
            return Task.FromResult<string?>(
                result[..MaxSafeOutputLength] + $"\n\n[Output truncated at {MaxSafeOutputLength:N0} characters for context safety]");
        }

        return Task.FromResult<string?>(null);
    }
}
