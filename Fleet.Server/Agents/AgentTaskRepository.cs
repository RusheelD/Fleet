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
            e.StartedAt, e.Duration, e.Progress
        )).ToList();
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetLogsByProjectIdAsync(string projectId)
    {
        var entities = await context.LogEntries
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .ToListAsync();

        return entities.Select(l => new LogEntryDto(l.Time, l.Agent, l.Level, l.Message)).ToList();
    }

    public async Task<IReadOnlyList<DashboardAgentDto>> GetDashboardAgentsByProjectIdAsync(string projectId)
    {
        var entities = await context.DashboardAgents
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .ToListAsync();

        return entities.Select(d => new DashboardAgentDto(d.Name, d.Status, d.Task, d.Progress)).ToList();
    }
}
