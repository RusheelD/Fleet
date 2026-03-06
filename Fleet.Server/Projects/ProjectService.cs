using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Data.Entities;
using Fleet.Server.GitHub;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Projects;

public class ProjectService(
    IProjectRepository projectRepository,
    IConnectionRepository connectionRepository,
    IWorkItemRepository workItemRepository,
    IAgentTaskRepository agentTaskRepository,
    IGitHubApiService gitHubApiService,
    IAuthService authService,
    ILogger<ProjectService> logger) : IProjectService
{
    // Backward-compatible constructor for existing tests/callers.
    public ProjectService(
        IProjectRepository projectRepository,
        IWorkItemRepository workItemRepository,
        IAgentTaskRepository agentTaskRepository,
        IGitHubApiService gitHubApiService,
        IAuthService authService,
        ILogger<ProjectService> logger)
        : this(
            projectRepository,
            new NullConnectionRepository(),
            workItemRepository,
            agentTaskRepository,
            gitHubApiService,
            authService,
            logger)
    {
    }

    private async Task<string> GetCurrentOwnerIdAsync() =>
        (await authService.GetCurrentUserIdAsync()).ToString();

    public async Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync()
    {
        logger.ProjectsRetrievingAll();
        var ownerId = await GetCurrentOwnerIdAsync();
        var projects = await projectRepository.GetAllByOwnerAsync(ownerId);
        var summaries = await workItemRepository.GetSummariesByProjectAsync();
        var agentSummaries = await agentTaskRepository.GetAgentSummariesByProjectAsync();

        return projects.Select(p =>
        {
            var wi = summaries.GetValueOrDefault(p.Id, new WorkItemSummaryDto(0, 0, 0));
            var agents = agentSummaries.GetValueOrDefault(p.Id, new AgentSummaryDto(0, 0));
            return p with { WorkItems = wi, Agents = agents };
        }).ToList();
    }

    public async Task<SlugCheckResult> CheckSlugAsync(string name)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectName"] = name
        });

        logger.ProjectsCheckingSlug(name.SanitizeForLogging());
        var slug = SlugHelper.GenerateSlug(name);
        if (string.IsNullOrEmpty(slug))
        {
            logger.ProjectsSlugEmpty(name.SanitizeForLogging());
            return new SlugCheckResult(slug, false);
        }

        var ownerId = await GetCurrentOwnerIdAsync();
        var available = await projectRepository.IsSlugAvailableAsync(ownerId, slug);
        logger.ProjectsSlugAvailability(slug.SanitizeForLogging(), available);
        return new SlugCheckResult(slug, available);
    }

    public async Task<ProjectDto> CreateProjectAsync(
        string title,
        string description,
        string repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null)
    {
        logger.ProjectsCreating(title.SanitizeForLogging());
        var ownerId = await GetCurrentOwnerIdAsync();

        if (string.IsNullOrWhiteSpace(repo))
            throw new InvalidOperationException("A GitHub repository must be selected to create a project.");

        if (!repo.Contains('/') || repo.StartsWith('/') || repo.EndsWith('/'))
            throw new InvalidOperationException("Repository must be in 'owner/repo' format.");

        if (!int.TryParse(ownerId, out var userId))
            throw new InvalidOperationException("Unable to resolve current user.");

        var linkedGitHub = await connectionRepository.GetByProviderAsync(userId, "GitHub");
        if (linkedGitHub is null)
            throw new InvalidOperationException("Link your GitHub account before creating a project.");

        return await projectRepository.CreateAsync(
            ownerId,
            title,
            description,
            repo,
            branchPattern,
            commitAuthorMode,
            commitAuthorName,
            commitAuthorEmail);
    }

    public async Task<ProjectDto?> UpdateProjectAsync(
        string id,
        string? title,
        string? description,
        string? repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = id
        });

        logger.ProjectsUpdating(id.SanitizeForLogging());
        var ownerId = await GetCurrentOwnerIdAsync();
        return await projectRepository.UpdateAsync(
            id,
            ownerId,
            title,
            description,
            repo,
            branchPattern,
            commitAuthorMode,
            commitAuthorName,
            commitAuthorEmail);
    }

    public async Task<bool> DeleteProjectAsync(string id)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = id
        });

        logger.ProjectsDeleting(id.SanitizeForLogging());
        var ownerId = await GetCurrentOwnerIdAsync();
        return await projectRepository.DeleteAsync(id, ownerId);
    }

    public async Task<ProjectDashboardDto?> GetDashboardBySlugAsync(string slug)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectSlug"] = slug
        });

        logger.ProjectsDashboardBySlug(slug.SanitizeForLogging());
        var ownerId = await GetCurrentOwnerIdAsync();
        var project = await projectRepository.GetBySlugAsync(slug, ownerId);
        if (project is null)
        {
            logger.ProjectsNotFoundBySlug(slug.SanitizeForLogging());
            return null;
        }

        return await BuildDashboard(project);
    }

    public async Task<ProjectDashboardDto?> GetDashboardAsync(string projectId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId
        });

        logger.ProjectsDashboardById(projectId.SanitizeForLogging());
        var ownerId = await GetCurrentOwnerIdAsync();
        var project = await projectRepository.GetByIdAsync(projectId, ownerId);
        if (project is null)
        {
            logger.ProjectsNotFoundById(projectId.SanitizeForLogging());
            return null;
        }

        return await BuildDashboard(project);
    }

    private async Task<ProjectDashboardDto> BuildDashboard(ProjectDto project)
    {
        var agents = await agentTaskRepository.GetDashboardAgentsByProjectIdAsync(project.Id);
        var agentSummary = await agentTaskRepository.GetAgentSummaryByProjectIdAsync(project.Id);
        var userId = await authService.GetCurrentUserIdAsync();
        // ── Real work item metrics from the database ──────────────
        var workItems = await workItemRepository.GetByProjectIdAsync(project.Id);
        workItems = await ApplyPullRequestReferencesAsync(project, userId, workItems);
        var totalItems = workItems.Count;
        var activeItems = workItems.Count(w =>
            IsActiveWorkItemState(w.State));
        var resolvedItems = workItems.Count(w =>
            w.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
            w.State.Equals("Resolved (AI)", StringComparison.OrdinalIgnoreCase));
        var closedItems = workItems.Count(w =>
            w.State.Equals("Closed", StringComparison.OrdinalIgnoreCase));
        var completedItems = resolvedItems + closedItems;
        var completionPct = totalItems > 0 ? Math.Round((double)completedItems / totalItems, 2) : 0;

        // ── Real GitHub stats ─────────────────────────────────────
        GitHubRepoStats gitHubStats;
        try
        {
            gitHubStats = !string.IsNullOrEmpty(project.Repo)
                ? await gitHubApiService.GetRepoStatsAsync(userId, project.Repo)
                : new GitHubRepoStats(0, 0, 0, []);
        }
        catch (Exception ex)
        {
            logger.ProjectsGitHubStatsFailed(ex, (project.Repo ?? string.Empty).SanitizeForLogging());
            gitHubStats = new GitHubRepoStats(0, 0, 0, []);
        }

        var metrics = new MetricDto[]
        {
            new("board", "Total Work Items", totalItems.ToString(),
                $"{activeItems} active · {resolvedItems} resolved · {closedItems} closed", null),
            new("bot", "Active Agents", agentSummary.Running.ToString(),
                $"of {agentSummary.Total} total", null),
            new("branch", "Pull Requests", (gitHubStats.OpenPullRequests + gitHubStats.MergedPullRequests).ToString(),
                $"{gitHubStats.OpenPullRequests} open · {gitHubStats.MergedPullRequests} merged", null),
            new("commit", "Commits (30d)", gitHubStats.RecentCommits.ToString(),
                "in the last 30 days", null),
            new("trending", "Completion", $"{(int)(completionPct * 100)}%", "", completionPct),
        };

        // ── Real activities from GitHub events ────────────────────
        var activities = gitHubStats.RecentEvents
            .Select(e => new ActivityDto(e.Icon, e.Text, FormatRelativeTime(e.Timestamp)))
            .ToArray();

        // If no GitHub events, show a helpful placeholder
        if (activities.Length == 0)
        {
            activities =
            [
                new ActivityDto("code", "No recent activity found. Connect GitHub and push some code!", "just now"),
            ];
        }

        return new ProjectDashboardDto(
            project.Id,
            project.Slug,
            project.Title,
            project.Repo ?? string.Empty,
            metrics,
            activities,
            [.. agents]
        );
    }

    private async Task<IReadOnlyList<WorkItemDto>> ApplyPullRequestReferencesAsync(
        ProjectDto project,
        int userId,
        IReadOnlyList<WorkItemDto> workItems)
    {
        if (string.IsNullOrWhiteSpace(project.Repo) || workItems.Count == 0)
            return workItems;

        IReadOnlyList<GitHubWorkItemReference> references;
        try
        {
            references = await gitHubApiService.GetWorkItemReferencesAsync(userId, project.Repo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync PR references for project {ProjectId}", project.Id);
            return workItems;
        }

        if (references.Count == 0)
            return workItems;

        var anyUpdates = false;
        var byWorkItemNumber = workItems.ToDictionary(w => w.WorkItemNumber);

        foreach (var group in references.GroupBy(r => r.WorkItemNumber))
        {
            if (!byWorkItemNumber.TryGetValue(group.Key, out var workItem))
                continue;

            var selectedReference = group
                .OrderByDescending(r => r.IsFixReference && r.IsMerged)
                .ThenByDescending(r => r.IsFixReference)
                .ThenByDescending(r => r.UpdatedAt)
                .First();

            var targetState = ResolveStateFromReference(workItem, selectedReference);
            var normalizedPrUrl = string.IsNullOrWhiteSpace(selectedReference.PullRequestUrl)
                ? null
                : selectedReference.PullRequestUrl;
            var stateChanged = !string.Equals(workItem.State, targetState, StringComparison.Ordinal);
            var linkChanged = !string.Equals(workItem.LinkedPullRequestUrl, normalizedPrUrl, StringComparison.Ordinal);
            if (!stateChanged && !linkChanged)
                continue;

            await workItemRepository.UpdateAsync(project.Id, workItem.WorkItemNumber,
                new UpdateWorkItemRequest(
                    Title: null,
                    Description: null,
                    Priority: null,
                    Difficulty: null,
                    State: stateChanged ? targetState : null,
                    AssignedTo: null,
                    Tags: null,
                    IsAI: null,
                    ParentWorkItemNumber: null,
                    LevelId: null,
                    LinkedPullRequestUrl: linkChanged ? normalizedPrUrl ?? string.Empty : null));

            byWorkItemNumber[group.Key] = workItem with
            {
                State = stateChanged ? targetState : workItem.State,
                LinkedPullRequestUrl = linkChanged ? normalizedPrUrl : workItem.LinkedPullRequestUrl,
            };
            anyUpdates = true;
        }

        return anyUpdates
            ? [.. byWorkItemNumber.Values.OrderBy(w => w.WorkItemNumber)]
            : workItems;
    }

    private static bool IsActiveWorkItemState(string state)
        => state.Equals("New", StringComparison.OrdinalIgnoreCase) ||
           state.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
           state.Equals("Planning (AI)", StringComparison.OrdinalIgnoreCase) ||
           state.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
           state.Equals("In Progress (AI)", StringComparison.OrdinalIgnoreCase) ||
           state.Equals("In-PR", StringComparison.OrdinalIgnoreCase) ||
           state.Equals("In-PR (AI)", StringComparison.OrdinalIgnoreCase);

    private static string ResolveStateFromReference(WorkItemDto workItem, GitHubWorkItemReference reference)
    {
        if (workItem.State.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            return workItem.State;

        if (reference.IsFixReference && reference.IsMerged)
            return workItem.IsAI ? "Resolved (AI)" : "Resolved";

        if (workItem.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
            workItem.State.Equals("Resolved (AI)", StringComparison.OrdinalIgnoreCase))
        {
            return workItem.State;
        }

        return workItem.IsAI ? "In-PR (AI)" : "In-PR";
    }

    private static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var diff = DateTimeOffset.UtcNow - timestamp;

        return diff.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)diff.TotalMinutes} min ago",
            < 1440 => diff.TotalHours < 2 ? "1 hour ago" : $"{(int)diff.TotalHours} hours ago",
            < 10080 => diff.TotalDays < 2 ? "1 day ago" : $"{(int)diff.TotalDays} days ago",
            _ => timestamp.ToString("MMM d, yyyy"),
        };
    }

    private sealed class NullConnectionRepository : IConnectionRepository
    {
        public Task<LinkedAccount?> GetByProviderAsync(int userId, string provider) => Task.FromResult<LinkedAccount?>(new LinkedAccount
        {
            Provider = provider,
            AccessToken = "test-token",
            UserProfileId = userId,
        });
        public Task<IReadOnlyList<LinkedAccountDto>> GetAllAsync(int userId) => Task.FromResult<IReadOnlyList<LinkedAccountDto>>([]);
        public Task<LinkedAccount> CreateAsync(LinkedAccount account) => Task.FromResult(account);
        public Task UpdateAsync(LinkedAccount account) => Task.CompletedTask;
        public Task DeleteAsync(LinkedAccount account) => Task.CompletedTask;
    }
}
