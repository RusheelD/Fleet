namespace Fleet.Server.Agents;

/// <summary>
/// Loads system prompt content for agent roles from prompt files.
/// </summary>
public interface IAgentPromptLoader
{
    /// <summary>Loads the system prompt for the given agent role.</summary>
    string GetPrompt(AgentRole role);

    /// <summary>Returns all known agent roles.</summary>
    IReadOnlyList<AgentRole> GetAllRoles();
}
