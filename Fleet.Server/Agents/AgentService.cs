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

    public async Task<int> ClearLogsAsync(string projectId)
    {
        logger.LogInformation("Clearing agent logs for project {ProjectId}", projectId.SanitizeForLogging());
        return await agentTaskRepository.ClearLogsByProjectIdAsync(projectId);
    }

    public async Task<int> ClearExecutionLogsAsync(string projectId, string executionId)
    {
        logger.LogInformation(
            "Clearing agent logs for project {ProjectId}, execution {ExecutionId}",
            projectId.SanitizeForLogging(),
            executionId.SanitizeForLogging());
        return await agentTaskRepository.ClearLogsByExecutionIdAsync(projectId, executionId);
    }

    public async Task<AgentExecutionMetrics?> GetExecutionMetricsAsync(string projectId, string executionId)
    {
        var execution = await agentTaskRepository.GetExecutionByIdAsync(projectId, executionId);
        if (execution is null) return null;

        var phaseResults = await agentTaskRepository.GetPhaseResultsAsync(executionId);

        var phases = phaseResults
            .OrderBy(p => p.PhaseOrder)
            .Select(p => new PhaseMetrics(
                p.Role,
                p.PhaseOrder,
                p.Success,
                p.ToolCallCount,
                p.InputTokens,
                p.OutputTokens,
                p.CompletedAt.HasValue ? p.CompletedAt.Value - p.StartedAt : null,
                p.Error))
            .ToList();

        var totalDuration = execution.StartedAtUtc.HasValue && execution.CompletedAtUtc.HasValue
            ? execution.CompletedAtUtc.Value - execution.StartedAtUtc.Value
            : execution.StartedAtUtc.HasValue
                ? DateTime.UtcNow - execution.StartedAtUtc.Value
                : (TimeSpan?)null;

        return new AgentExecutionMetrics(
            execution.Id,
            execution.Status,
            execution.Progress,
            phases.Sum(p => p.ToolCallCount),
            phases.Sum(p => (long)p.InputTokens),
            phases.Sum(p => (long)p.OutputTokens),
            totalDuration,
            phases);
    }
}
