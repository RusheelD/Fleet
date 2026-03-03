using Fleet.Server.Models;

namespace Fleet.Server.Agents;

public interface IAgentTaskRepository
{
    Task<IReadOnlyList<AgentExecutionDto>> GetExecutionsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<LogEntryDto>> GetLogsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<DashboardAgentDto>> GetDashboardAgentsByProjectIdAsync(string projectId);
}
