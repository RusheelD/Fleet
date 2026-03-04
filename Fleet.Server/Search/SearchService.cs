using Fleet.Server.Data;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Search;

public class SearchService(FleetDbContext context, ILogger<SearchService> logger) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string? query, string? type)
    {
        logger.SearchStarted((query ?? string.Empty).SanitizeForLogging(), (type ?? "all").SanitizeForLogging());
        var results = new List<SearchResultDto>();

        var includeAll = string.IsNullOrWhiteSpace(type) || type == "all";

        // Search projects
        if (includeAll || type == "projects")
        {
            var projects = await context.Projects.AsNoTracking().ToListAsync();
            results.AddRange(projects.Select(p =>
                new SearchResultDto("project", p.Title, p.Description, $"Last active {p.LastActivity}")));
        }

        // Search work items (include Project for title)
        if (includeAll || type == "workitems")
        {
            var workItems = await context.WorkItems
                .AsNoTracking()
                .Include(w => w.Project)
                .ToListAsync();
            results.AddRange(workItems.Select(w =>
                new SearchResultDto("workitem", $"#{w.Id} \u2014 {w.Title}",
                    $"{w.State} · Priority {w.Priority}", w.Project.Title)));
        }

        // Search chat sessions (include Project for title)
        if (includeAll || type == "chats")
        {
            var chats = await context.ChatSessions
                .AsNoTracking()
                .Include(c => c.Project)
                .ToListAsync();
            results.AddRange(chats.Select(c =>
                new SearchResultDto("chat", c.Title, c.LastMessage,
                    $"{c.Project.Title} · {c.Timestamp}")));
        }

        // Search agent executions
        if (includeAll || type == "agents")
        {
            var agents = await context.AgentExecutions.AsNoTracking().ToListAsync();
            results.AddRange(agents.Select(e =>
                new SearchResultDto("agent", e.WorkItemTitle,
                    $"{e.Status} · {(int)(e.Progress * 100)}% complete",
                    $"Work Item #{e.WorkItemId}")));
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
