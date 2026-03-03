using Fleet.Server.Models;

namespace Fleet.Server.Agents;

public class AgentService(
    IAgentTaskRepository agentTaskRepository,
    ILogger<AgentService> logger) : IAgentService
{
    public async Task<IReadOnlyList<AgentExecutionDto>> GetExecutionsAsync(string projectId)
    {
        logger.LogInformation("Retrieving agent executions for project {ProjectId}", projectId);
        return await agentTaskRepository.GetExecutionsByProjectIdAsync(projectId);
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetLogsAsync(string projectId)
    {
        logger.LogInformation("Retrieving agent logs for project {ProjectId}", projectId);
        return await agentTaskRepository.GetLogsByProjectIdAsync(projectId);
    }
}
