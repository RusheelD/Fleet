using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
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
            .OrderByDescending(e => e.StartedAtUtc)
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

        var childrenByParentExecutionId = entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.ParentExecutionId))
            .GroupBy(entity => entity.ParentExecutionId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(entity => entity.StartedAtUtc)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        AgentExecutionDto BuildDto(Data.Entities.AgentExecution entity)
        {
            reviewSummariesByExecution.TryGetValue(entity.Id, out var reviewSummary);
            var subFlows = childrenByParentExecutionId.TryGetValue(entity.Id, out var children)
                ? children.Select(BuildDto).ToArray()
                : [];

            return new AgentExecutionDto(
                entity.Id,
                entity.WorkItemId,
                entity.WorkItemTitle,
                string.IsNullOrWhiteSpace(entity.ExecutionMode) ? AgentExecutionModes.Standard : entity.ExecutionMode,
                entity.Status,
                NormalizeAgentsForExecutionStatus(entity).ToArray(),
                entity.StartedAt,
                ComputeDuration(entity),
                entity.Progress,
                entity.BranchName,
                entity.PullRequestUrl,
                entity.CurrentPhase,
                reviewSummary?.AutomaticLoopCount ?? 0,
                reviewSummary?.LastRecommendation,
                entity.ParentExecutionId,
                subFlows);
        }

        return entities
            .Where(entity => string.IsNullOrWhiteSpace(entity.ParentExecutionId))
            .Select(BuildDto)
            .ToList();
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

        var descendantExecutionIds = await CollectDescendantExecutionIdsAsync(projectId, executionId);
        var executionIdsToDelete = descendantExecutionIds
            .Append(executionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var logs = await context.LogEntries
            .Where(l => l.ProjectId == projectId && l.ExecutionId != null && executionIdsToDelete.Contains(l.ExecutionId))
            .ToListAsync();
        var deletedLogCount = logs.Count;

        var phaseResults = await context.AgentPhaseResults
            .Where(result => executionIdsToDelete.Contains(result.ExecutionId))
            .ToListAsync();

        if (phaseResults.Count > 0)
            context.AgentPhaseResults.RemoveRange(phaseResults);

        if (deletedLogCount > 0)
            context.LogEntries.RemoveRange(logs);

        var executions = await context.AgentExecutions
            .Where(result => result.ProjectId == projectId && executionIdsToDelete.Contains(result.Id))
            .ToListAsync();
        if (executions.Count > 0)
            context.AgentExecutions.RemoveRange(executions);

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
            .CountAsync(e => e.ProjectId == projectId && e.ParentExecutionId == null);

        var running = await context.AgentExecutions
            .AsNoTracking()
            .CountAsync(e => e.ProjectId == projectId && e.ParentExecutionId == null && e.Status == "running");

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
                Total = g.Count(e => e.ParentExecutionId == null),
                Running = g.Count(e => e.ParentExecutionId == null && e.Status == "running"),
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

    private async Task<List<string>> CollectDescendantExecutionIdsAsync(string projectId, string parentExecutionId)
    {
        var descendants = new List<string>();
        var frontier = new Queue<string>();
        frontier.Enqueue(parentExecutionId);

        while (frontier.Count > 0)
        {
            var currentParentId = frontier.Dequeue();
            var childIds = await context.AgentExecutions
                .AsNoTracking()
                .Where(execution =>
                    execution.ProjectId == projectId &&
                    execution.ParentExecutionId == currentParentId)
                .Select(execution => execution.Id)
                .ToListAsync();

            foreach (var childId in childIds)
            {
                descendants.Add(childId);
                frontier.Enqueue(childId);
            }
        }

        return descendants;
    }

    public async Task<AgentExecution?> GetExecutionByIdAsync(string projectId, string executionId)
    {
        return await context.AgentExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ProjectId == projectId && e.Id == executionId);
    }

    public async Task<IReadOnlyList<AgentPhaseResult>> GetPhaseResultsAsync(string executionId)
    {
        return await context.AgentPhaseResults
            .AsNoTracking()
            .Where(p => p.ExecutionId == executionId)
            .OrderBy(p => p.PhaseOrder)
            .ToListAsync();
    }
}
