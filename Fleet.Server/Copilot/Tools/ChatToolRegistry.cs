using Fleet.Server.LLM;

namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Collects all registered <see cref="IChatTool"/> implementations
/// and provides lookup + LLM tool definitions.
/// </summary>
public class ChatToolRegistry
{
    private readonly Dictionary<string, IChatTool> _tools;

    public ChatToolRegistry(IEnumerable<IChatTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>All registered tools.</summary>
    public IReadOnlyList<IChatTool> All => [.. _tools.Values];

    /// <summary>Look up a tool by name (case-insensitive).</summary>
    public IChatTool? Get(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>Convert tools to LLM definitions, optionally excluding write tools.</summary>
    public IReadOnlyList<LLMToolDefinition> ToLLMDefinitions(
        bool includeWriteTools = true,
        bool bulkOnly = false,
        bool includeGlobalRepoTools = true,
        bool includeNormalChatWriteTools = true,
        bool workItemGenerationOnly = false) =>
        All.Where(t => includeWriteTools || !t.IsWriteTool || AlwaysAvailableWriteTools.Contains(t.Name) || (includeNormalChatWriteTools && t.AllowInNormalChat))
           .Where(t => !workItemGenerationOnly || !t.IsWriteTool || WorkItemGenerationWriteTools.Contains(t.Name))
           .Where(t => !bulkOnly || !SingleItemWriteTools.Contains(t.Name))
           .Where(t => includeGlobalRepoTools || !GlobalOnlyTools.Contains(t.Name))
           .Select(t => new LLMToolDefinition(t.Name, t.Description, t.ParametersJsonSchema))
           .ToList();

    /// <summary>
    /// Single-item write tool names that have bulk equivalents.
    /// When <c>bulkOnly</c> is true, these are excluded so the LLM
    /// is forced to batch operations and reduce API round-trips.
    /// </summary>
    private static readonly HashSet<string> SingleItemWriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_work_item",
        "update_work_item",
        "delete_work_item",
    };

    private static readonly HashSet<string> GlobalOnlyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "list_github_repos",
        "create_project",
    };

    private static readonly HashSet<string> AlwaysAvailableWriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_project",
    };

    /// <summary>
    /// Work-item generation should only expose write tools that mutate the backlog itself.
    /// This prevents unrelated write-capable tools and system MCP actions from hijacking the
    /// generation flow or causing request-shape regressions.
    /// </summary>
    private static readonly HashSet<string> WorkItemGenerationWriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "bulk_create_work_items",
        "bulk_update_work_items",
        "bulk_delete_work_items",
        "try_bulk_update_work_items",
    };
}
