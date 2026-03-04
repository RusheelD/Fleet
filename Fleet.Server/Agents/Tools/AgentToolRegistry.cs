using Fleet.Server.LLM;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Collects all registered <see cref="IAgentTool"/> implementations
/// and provides lookup + LLM tool definitions for the agent pipeline.
/// </summary>
public class AgentToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools;

    public AgentToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>All registered agent tools.</summary>
    public IReadOnlyList<IAgentTool> All => [.. _tools.Values];

    /// <summary>Look up a tool by name (case-insensitive).</summary>
    public IAgentTool? Get(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>Convert all tools to LLM tool definitions.</summary>
    public IReadOnlyList<LLMToolDefinition> ToLLMDefinitions() =>
        All.Select(t => new LLMToolDefinition(t.Name, t.Description, t.ParametersJsonSchema))
           .ToList();
}
