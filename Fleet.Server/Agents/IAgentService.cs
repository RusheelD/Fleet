using Fleet.Server.Models;

namespace Fleet.Server.Agents;

public interface IAgentService
{
    Task<IReadOnlyList<AgentExecutionDto>> GetExecutionsAsync(string projectId);
    Task<IReadOnlyList<LogEntryDto>> GetLogsAsync(string projectId);
    Task<int> ClearLogsAsync(string projectId);
}
