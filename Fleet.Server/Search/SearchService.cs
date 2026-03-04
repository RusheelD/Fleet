using Fleet.Server.Data;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Search;

public class SearchService(FleetDbContext context, ILogger<SearchService> logger) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string ownerId, string? query, string? type)
    {
        logger.SearchStarted((query ?? string.Empty).SanitizeForLogging(), (type ?? "all").SanitizeForLogging());
        var results = new List<SearchResultDto>();

        var includeAll = string.IsNullOrWhiteSpace(type) || type == "all";

        // Search projects (scoped to current user)
        if (includeAll || type == "projects")
        {
            var projects = await context.Projects.AsNoTracking()
                .Where(p => p.OwnerId == ownerId)
                .ToListAsync();
            results.AddRange(projects.Select(p =>
                new SearchResultDto("project", p.Title, p.Description, $"Last active {p.LastActivity}", p.Slug)));
        }

        // Search work items (scoped to user's projects)
        if (includeAll || type == "workitems")
        {
            var workItems = await context.WorkItems
                .AsNoTracking()
                .Include(w => w.Project)
                .Where(w => w.Project.OwnerId == ownerId)
                .ToListAsync();
            results.AddRange(workItems.Select(w =>
                new SearchResultDto("workitem", $"#{w.WorkItemNumber} \u2014 {w.Title}",
                    $"{w.State} \u00b7 Priority {w.Priority}", w.Project.Title, w.Project.Slug)));
        }

        // Search chat sessions (scoped to user's projects)
        if (includeAll || type == "chats")
        {
            var chats = await context.ChatSessions
                .AsNoTracking()
                .Include(c => c.Project)
                .Where(c => c.Project.OwnerId == ownerId)
                .ToListAsync();
            results.AddRange(chats.Select(c =>
                new SearchResultDto("chat", c.Title, c.LastMessage,
                    $"{c.Project.Title} \u00b7 {c.Timestamp}", c.Project.Slug)));
        }

        // Search agent executions (scoped to user's projects)
        if (includeAll || type == "agents")
        {
            var agents = await context.AgentExecutions
                .AsNoTracking()
                .Include(e => e.Project)
                .Where(e => e.Project.OwnerId == ownerId)
                .ToListAsync();
            results.AddRange(agents.Select(e =>
                new SearchResultDto("agent", e.WorkItemTitle,
                    $"{e.Status} \u00b7 {(int)(e.Progress * 100)}% complete",
                    $"Work Item #{e.WorkItemId}", e.Project.Slug)));
        }

        // Apply text filter in memory
        if (!string.IsNullOrWhiteSpace(query))
        {
            results = results
                .Where(r =>
                    r.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        logger.SearchCompleted(results.Count);
        return results;
    }
}
