using Fleet.Server.Data;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Search;

public class SearchService(FleetDbContext context, ILogger<SearchService> logger) : ISearchService
{
    private const int MaxResultsPerCategory = 25;

    // PostgreSQL supports ILike for case-insensitive matching.
    // InMemory (tests) doesn't, so we detect and fall back to Contains.
    private readonly bool _useILike = context.Database.ProviderName?.Contains("Npgsql") == true;

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string ownerId, string? query, string? type)
    {
        var normalizedType = NormalizeType(type);
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        logger.SearchStarted((query ?? string.Empty).SanitizeForLogging(), normalizedType.SanitizeForLogging());
        var includeAll = normalizedType == "all";

        // Run search queries sequentially — all methods share a scoped DbContext
        // which does not support concurrent async operations.
        var results = new List<SearchResultDto>();

        if (includeAll || normalizedType == "projects")
            results.AddRange(await SearchProjectsAsync(ownerId, normalizedQuery));

        if (includeAll || normalizedType == "workitems")
            results.AddRange(await SearchWorkItemsAsync(ownerId, normalizedQuery));

        if (includeAll || normalizedType == "chats")
            results.AddRange(await SearchChatsAsync(ownerId, normalizedQuery));

        if (includeAll || normalizedType == "agents")
            results.AddRange(await SearchAgentsAsync(ownerId, normalizedQuery));

        logger.SearchCompleted(results.Count);
        return results;
    }

    private async Task<IReadOnlyList<SearchResultDto>> SearchProjectsAsync(string ownerId, string? query)
    {
        var projects = context.Projects
            .AsNoTracking()
            .Where(project => project.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (_useILike)
            {
                var pattern = $"%{query}%";
                projects = projects.Where(project =>
                    EF.Functions.ILike(project.Title, pattern) ||
                    EF.Functions.ILike(project.Description, pattern) ||
                    EF.Functions.ILike(project.Slug, pattern) ||
                    EF.Functions.ILike(project.Repo, pattern));
            }
            else
            {
                projects = projects.Where(project =>
                    project.Title.Contains(query) ||
                    project.Description.Contains(query) ||
                    project.Slug.Contains(query) ||
                    project.Repo.Contains(query));
            }
        }

        return await projects
            .OrderBy(project => project.Title)
            .Take(MaxResultsPerCategory)
            .Select(project => new SearchResultDto(
                "project",
                project.Title,
                project.Description,
                $"Last active {project.LastActivity}",
                project.Slug))
            .ToListAsync();
    }

    private async Task<IReadOnlyList<SearchResultDto>> SearchWorkItemsAsync(string ownerId, string? query)
    {
        var workItems = context.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Project.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (_useILike)
            {
                var pattern = $"%{query}%";
                workItems = workItems.Where(workItem =>
                    EF.Functions.ILike(workItem.Title, pattern) ||
                    EF.Functions.ILike(workItem.Description, pattern) ||
                    EF.Functions.ILike(workItem.AcceptanceCriteria, pattern) ||
                    EF.Functions.ILike(workItem.Project.Title, pattern));
            }
            else
            {
                workItems = workItems.Where(workItem =>
                    workItem.Title.Contains(query) ||
                    workItem.Description.Contains(query) ||
                    workItem.AcceptanceCriteria.Contains(query) ||
                    workItem.Project.Title.Contains(query));
            }
        }

        return await workItems
            .OrderBy(workItem => workItem.Title)
            .Take(MaxResultsPerCategory)
            .Select(workItem => new SearchResultDto(
                "workitem",
                $"#{workItem.WorkItemNumber} \u2014 {workItem.Title}",
                $"{workItem.State} \u00b7 Priority {workItem.Priority}",
                workItem.Project.Title,
                workItem.Project.Slug))
            .ToListAsync();
    }

    private async Task<IReadOnlyList<SearchResultDto>> SearchChatsAsync(string ownerId, string? query)
    {
        var chats = context.ChatSessions
            .AsNoTracking()
            .Where(chat => chat.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (_useILike)
            {
                var pattern = $"%{query}%";
                chats = chats.Where(chat =>
                    EF.Functions.ILike(chat.Title, pattern) ||
                    EF.Functions.ILike(chat.LastMessage, pattern) ||
                    (chat.Project != null && EF.Functions.ILike(chat.Project.Title, pattern)));
            }
            else
            {
                chats = chats.Where(chat =>
                    chat.Title.Contains(query) ||
                    chat.LastMessage.Contains(query) ||
                    (chat.Project != null && chat.Project.Title.Contains(query)));
            }
        }

        return await chats
            .OrderByDescending(chat => chat.IsActive)
            .ThenByDescending(chat => chat.GenerationUpdatedAtUtc)
            .ThenBy(chat => chat.Title)
            .Take(MaxResultsPerCategory)
            .Select(chat => new SearchResultDto(
                "chat",
                chat.Title,
                chat.LastMessage,
                $"{(chat.Project != null ? chat.Project.Title : "Global chat")} \u00b7 {chat.Timestamp}",
                chat.Project != null ? chat.Project.Slug : null))
            .ToListAsync();
    }

    private async Task<IReadOnlyList<SearchResultDto>> SearchAgentsAsync(string ownerId, string? query)
    {
        var agents = context.AgentExecutions
            .AsNoTracking()
            .Where(agent => agent.Project.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (_useILike)
            {
                var pattern = $"%{query}%";
                agents = agents.Where(agent =>
                    EF.Functions.ILike(agent.WorkItemTitle, pattern) ||
                    EF.Functions.ILike(agent.Status, pattern) ||
                    (agent.CurrentPhase != null && EF.Functions.ILike(agent.CurrentPhase, pattern)) ||
                    EF.Functions.ILike(agent.Project.Title, pattern));
            }
            else
            {
                agents = agents.Where(agent =>
                    agent.WorkItemTitle.Contains(query) ||
                    agent.Status.Contains(query) ||
                    (agent.CurrentPhase != null && agent.CurrentPhase.Contains(query)) ||
                    agent.Project.Title.Contains(query));
            }
        }

        return await agents
            .OrderByDescending(agent => agent.StartedAtUtc)
            .ThenBy(agent => agent.WorkItemTitle)
            .Take(MaxResultsPerCategory)
            .Select(agent => new SearchResultDto(
                "agent",
                agent.WorkItemTitle,
                $"{agent.Status} \u00b7 {(int)(agent.Progress * 100)}% complete",
                $"Work Item #{agent.WorkItemId}",
                agent.Project.Slug))
            .ToListAsync();
    }

    private static string NormalizeType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        null or "" => "all",
        "all" => "all",
        "project" or "projects" => "projects",
        "workitem" or "workitems" => "workitems",
        "chat" or "chats" => "chats",
        "agent" or "agents" => "agents",
        _ => "all",
    };
}
