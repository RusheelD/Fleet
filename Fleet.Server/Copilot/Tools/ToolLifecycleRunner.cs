namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Runs registered lifecycle hooks in order around tool execution.
/// </summary>
public class ToolLifecycleRunner(IEnumerable<IToolLifecycleHook> hooks)
{
    private readonly IToolLifecycleHook[] _hooks = hooks.OrderBy(h => h.Order).ToArray();

    /// <summary>
    /// Run all BeforeExecute hooks. Returns an override result if any hook vetoes
    /// the tool call, or null to proceed with normal execution.
    /// </summary>
    public async Task<string?> RunBeforeExecuteAsync(
        ToolHookContext context, CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            var result = await hook.BeforeExecuteAsync(context, cancellationToken);
            if (result is not null)
                return result;
        }
        return null;
    }

    /// <summary>
    /// Run all AfterExecute hooks. Returns the final (possibly modified) result.
    /// </summary>
    public async Task<string> RunAfterExecuteAsync(
        ToolHookContext context, string result, CancellationToken cancellationToken = default)
    {
        foreach (var hook in _hooks)
        {
            var modified = await hook.AfterExecuteAsync(context, result, cancellationToken);
            if (modified is not null)
                result = modified;
        }
        return result;
    }
}
