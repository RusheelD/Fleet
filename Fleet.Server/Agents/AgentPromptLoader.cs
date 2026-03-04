using System.Collections.Concurrent;

namespace Fleet.Server.Agents;

/// <summary>
/// Loads agent role prompt files from the <c>docs/agents/</c> folder.
/// Prompts are cached in memory after first load.
/// </summary>
public class AgentPromptLoader : IAgentPromptLoader
{
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Known agent roles mapped to their prompt file names.
    /// </summary>
    public static IReadOnlyDictionary<AgentRole, string> RoleFileNames { get; } = new Dictionary<AgentRole, string>
    {
        [AgentRole.Manager] = "manager.prompt.md",
        [AgentRole.Planner] = "planner.prompt.md",
        [AgentRole.Contracts] = "contracts.prompt.md",
        [AgentRole.Backend] = "backend.prompt.md",
        [AgentRole.Frontend] = "frontend.prompt.md",
        [AgentRole.Testing] = "testing.prompt.md",
        [AgentRole.Styling] = "styling.prompt.md",
        [AgentRole.Consolidation] = "consolidation.prompt.md",
        [AgentRole.Review] = "review.prompt.md",
        [AgentRole.Documentation] = "documentation.prompt.md",
    };

    public string GetPrompt(AgentRole role)
    {
        var fileName = RoleFileNames[role];
        return _cache.GetOrAdd(fileName, static name =>
        {
            var path = ResolvePromptPath(name);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Agent prompt file not found: {path}");

            return File.ReadAllText(path);
        });
    }

    public IReadOnlyList<AgentRole> GetAllRoles() => [.. RoleFileNames.Keys];

    /// <summary>
    /// Resolves the absolute path to a prompt file, checking the published
    /// output directory first, then falling back to the dev-time project root.
    /// </summary>
    private static string ResolvePromptPath(string fileName)
    {
        // Published layout: alongside the assembly
        var publishedPath = Path.Combine(AppContext.BaseDirectory, "docs", "agents", fileName);
        if (File.Exists(publishedPath))
            return publishedPath;

        // Development: walk up from cwd to find the repo root docs/agents/ folder
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "agents", fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        // Return the published path even if missing — caller will get FileNotFoundException
        return publishedPath;
    }
}
