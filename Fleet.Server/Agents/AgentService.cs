using Fleet.Server.Models;
using Fleet.Server.Logging;

namespace Fleet.Server.Agents;

public class AgentService(
    IAgentTaskRepository agentTaskRepository,
    ILogger<AgentService> logger) : IAgentService
{
    public async Task<IReadOnlyList<AgentExecutionDto>> GetExecutionsAsync(string projectId)
    {
        logger.AgentsExecutionsRetrieving(projectId.SanitizeForLogging());
        return await agentTaskRepository.GetExecutionsByProjectIdAsync(projectId);
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetLogsAsync(string projectId)
    {
        logger.AgentsLogsRetrieving(projectId.SanitizeForLogging());
        return await agentTaskRepository.GetLogsByProjectIdAsync(projectId);
    }
}
