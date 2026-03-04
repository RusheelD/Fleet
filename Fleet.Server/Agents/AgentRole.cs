namespace Fleet.Server.Agents;

/// <summary>
/// Defines the agent roles in Fleet's sequential execution pipeline.
/// Each role maps to a <c>docs/agents/{role}.prompt.md</c> file.
/// </summary>
public enum AgentRole
{
    Manager,
    Planner,
    Contracts,
    Backend,
    Frontend,
    Testing,
    Styling,
    Consolidation,
    Review,
    Documentation,
}
