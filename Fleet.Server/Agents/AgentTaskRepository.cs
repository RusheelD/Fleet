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

        return entities.Select(e => new AgentExecutionDto(
            e.Id, e.WorkItemId, e.WorkItemTitle, e.Status,
            e.Agents.Select(a => new AgentInfoDto(a.Role, a.Status, a.CurrentTask, a.Progress)).ToArray(),
            e.StartedAt, ComputeDuration(e), e.Progress
        )).ToList();
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetLogsByProjectIdAsync(string projectId)
    {
        var entities = await context.LogEntries
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .ToListAsync();

        return entities.Select(l => new LogEntryDto(l.Time, l.Agent, l.Level, l.Message, l.IsDetailed)).ToList();
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
}
