using Fleet.Server.LLM;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Collects all registered <see cref="IAgentTool"/> implementations
/// and provides lookup + LLM tool definitions for the agent pipeline.
/// </summary>
public class AgentToolRegistry
{
    private static readonly HashSet<string> ManagerAllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "list_directory",
        "read_file",
        "search_files",
        "get_change_summary",
        "report_progress",
    };

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

    /// <summary>
    /// Convert role-allowed tools to LLM tool definitions.
    /// Manager is orchestration-only and cannot access coding tools.
    /// </summary>
    public IReadOnlyList<LLMToolDefinition> ToLLMDefinitions(AgentRole role) =>
        All
            .Where(t => IsToolAllowed(role, t.Name))
            .Select(t => new LLMToolDefinition(t.Name, t.Description, t.ParametersJsonSchema))
            .ToList();

    public bool IsToolAllowed(AgentRole role, string toolName) =>
        role switch
        {
            AgentRole.Manager => ManagerAllowedTools.Contains(toolName),
            _ => true,
        };
}
