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
    public IReadOnlyList<LLMToolDefinition> ToLLMDefinitions(bool includeWriteTools = true) =>
        All.Where(t => includeWriteTools || !t.IsWriteTool)
           .Select(t => new LLMToolDefinition(t.Name, t.Description, t.ParametersJsonSchema))
           .ToList();
}
