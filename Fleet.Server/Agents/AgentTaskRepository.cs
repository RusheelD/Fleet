using Fleet.Server.Data;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Agents;

public class AgentTaskRepository(FleetDbContext context) : IAgentTaskRepository
{
    public async Task<IReadOnlyList<AgentExecutionDto>> GetExecutionsByProjectIdAsync(string projectId)
    {
        var entities = await context.AgentExecutions
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .ToListAsync();

        var executionIds = entities.Select(entity => entity.Id).ToArray();
        var reviewPhaseResults = executionIds.Length == 0
            ? []
            : await context.AgentPhaseResults
                .AsNoTracking()
                .Where(result =>
                    executionIds.Contains(result.ExecutionId) &&
                    result.Success &&
                    result.Role == AgentRole.Review.ToString())
                .OrderBy(result => result.ExecutionId)
                .ThenBy(result => result.PhaseOrder)
                .ToListAsync();

        var reviewSummariesByExecution = reviewPhaseResults
            .GroupBy(result => result.ExecutionId)
            .ToDictionary(
                group => group.Key,
                group => ReviewFeedbackLoopPlanner.SummarizeExecutionReviews(group));

        return entities.Select(e =>
        {
            reviewSummariesByExecution.TryGetValue(e.Id, out var reviewSummary);
            return new AgentExecutionDto(
                e.Id,
                e.WorkItemId,
                e.WorkItemTitle,
                e.Status,
                NormalizeAgentsForExecutionStatus(e).ToArray(),
                e.StartedAt,
                ComputeDuration(e),
                e.Progress,
                e.BranchName,
                e.PullRequestUrl,
                e.CurrentPhase,
                reviewSummary?.AutomaticLoopCount ?? 0,
                reviewSummary?.LastRecommendation);
        }).ToList();
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetLogsByProjectIdAsync(string projectId)
    {
        var entities = await context.LogEntries
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .OrderBy(l => l.Time)
            .ThenBy(l => l.Id)
            .ToListAsync();

        return entities.Select(l => new LogEntryDto(l.Time, l.Agent, l.Level, l.Message, l.IsDetailed, l.ExecutionId)).ToList();
    }

    public async Task<int> ClearLogsByProjectIdAsync(string projectId)
    {
        var logs = await context.LogEntries
            .Where(l => l.ProjectId == projectId)
            .ToListAsync();
        if (logs.Count == 0)
            return 0;

        context.LogEntries.RemoveRange(logs);
        await context.SaveChangesAsync();
        return logs.Count;
    }

    public async Task<int> ClearLogsByExecutionIdAsync(string projectId, string executionId)
    {
        var logs = await context.LogEntries
            .Where(l => l.ProjectId == projectId && l.ExecutionId == executionId)
            .ToListAsync();
        if (logs.Count == 0)
            return 0;

        context.LogEntries.RemoveRange(logs);
        await context.SaveChangesAsync();
        return logs.Count;
    }

    public async Task<AgentExecutionDeletionResult?> DeleteExecutionAsync(string projectId, string executionId)
    {
        var execution = await context.AgentExecutions
            .FirstOrDefaultAsync(e => e.ProjectId == projectId && e.Id == executionId);
        if (execution is null)
            return null;

        var logs = await context.LogEntries
            .Where(l => l.ProjectId == projectId && l.ExecutionId == executionId)
            .ToListAsync();
        var deletedLogCount = logs.Count;

        var phaseResults = await context.AgentPhaseResults
            .Where(result => result.ExecutionId == executionId)
            .ToListAsync();

        if (phaseResults.Count > 0)
            context.AgentPhaseResults.RemoveRange(phaseResults);

        if (deletedLogCount > 0)
            context.LogEntries.RemoveRange(logs);

        context.AgentExecutions.Remove(execution);
        await context.SaveChangesAsync();

        return new AgentExecutionDeletionResult(executionId, deletedLogCount);
    }

    public async Task<IReadOnlyList<DashboardAgentDto>> GetDashboardAgentsByProjectIdAsync(string projectId)
    {
        // Derive dashboard agents from real execution data instead of the static DashboardAgents table.
        var runningExecutions = await context.AgentExecutions
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Status == "running")
            .ToListAsync();

        if (runningExecutions.Count > 0)
        {
            // Aggregate sub-agents across all running executions
            return runningExecutions
                .SelectMany(e => e.Agents)
                .Select(a => new DashboardAgentDto(
                    $"{a.Role} Agent",
                    a.Status,
                    a.CurrentTask,
                    a.Progress))
                .ToList();
        }

        // No running executions — show the most recent execution's agents as idle
        var recentExecution = await context.AgentExecutions
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (recentExecution is null)
            return [];

        return recentExecution.Agents
            .Select(a => new DashboardAgentDto(
                $"{a.Role} Agent",
                "idle",
                a.Status == "completed" ? "Completed" : a.CurrentTask,
                a.Status == "completed" ? 1.0 : a.Progress))
            .ToList();
    }

    public async Task<AgentSummaryDto> GetAgentSummaryByProjectIdAsync(string projectId)
    {
        var total = await context.AgentExecutions
            .AsNoTracking()
            .CountAsync(e => e.ProjectId == projectId);

        var running = await context.AgentExecutions
            .AsNoTracking()
            .CountAsync(e => e.ProjectId == projectId && e.Status == "running");

        return new AgentSummaryDto(total, running);
    }

    public async Task<Dictionary<string, AgentSummaryDto>> GetAgentSummariesByProjectAsync()
    {
        var groups = await context.AgentExecutions
            .AsNoTracking()
            .GroupBy(e => e.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Running = g.Count(e => e.Status == "running"),
            })
            .ToListAsync();

        return groups.ToDictionary(
            g => g.ProjectId,
            g => new AgentSummaryDto(g.Total, g.Running));
    }

    /// <summary>
    /// Returns the persisted duration for completed/failed executions,
    /// or computes elapsed time for running executions.
    /// </summary>
    private static string ComputeDuration(Data.Entities.AgentExecution execution)
    {
        if (execution.Status != "running" || !execution.StartedAtUtc.HasValue)
            return execution.Duration;

        var elapsed = DateTime.UtcNow - execution.StartedAtUtc.Value;
        return elapsed.TotalMinutes < 1
            ? $"{elapsed.TotalSeconds:F0}s"
            : $"{elapsed.TotalMinutes:F1}m";
    }

    private static IEnumerable<AgentInfoDto> NormalizeAgentsForExecutionStatus(Data.Entities.AgentExecution execution)
    {
        var executionStatus = execution.Status ?? string.Empty;
        var forceTerminal = executionStatus.Equals("failed", StringComparison.OrdinalIgnoreCase) ||
                            executionStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase);

        if (!forceTerminal)
        {
            return execution.Agents.Select(a => new AgentInfoDto(a.Role, a.Status, a.CurrentTask, a.Progress));
        }

        var terminalStatus = executionStatus.ToLowerInvariant();
        var terminalTask = terminalStatus == "failed" ? "Failed" : "Cancelled";

        return execution.Agents.Select(a => new AgentInfoDto(a.Role, terminalStatus, terminalTask, 0));
    }
}
