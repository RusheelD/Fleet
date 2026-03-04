using Fleet.Server.Models;

namespace Fleet.Server.Agents;

public interface IAgentTaskRepository
{
    Task<IReadOnlyList<AgentExecutionDto>> GetExecutionsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<LogEntryDto>> GetLogsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<DashboardAgentDto>> GetDashboardAgentsByProjectIdAsync(string projectId);

    /// <summary>Computes total/running execution counts for a single project.</summary>
    Task<AgentSummaryDto> GetAgentSummaryByProjectIdAsync(string projectId);

    /// <summary>Computes total/running execution counts for every project (keyed by project ID).</summary>
    Task<Dictionary<string, AgentSummaryDto>> GetAgentSummariesByProjectAsync();
}
