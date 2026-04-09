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

        // Run summary queries sequentially — repositories share a scoped DbContext
        // which does not support concurrent async operations.
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

        // ── Work items: load for PR-sync side-effect, count for metrics ──
        var workItems = await workItemRepository.GetByProjectIdAsync(project.Id);
        workItems = await ApplyPullRequestReferencesAsync(project, userId, workItems);

        var totalItems = workItems.Count;
        var activeItems = workItems.Count(w => w.State is "New" or "Active" or "Planning (AI)"
            or "In Progress" or "In Progress (AI)" or "In-PR" or "In-PR (AI)");
        var resolvedItems = workItems.Count(w => w.State is "Resolved" or "Resolved (AI)");
        var closedItems = workItems.Count(w => w.State is "Closed");
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

        var anyUpdates = false;
        var byWorkItemNumber = workItems.ToDictionary(w => w.WorkItemNumber);
        var latestReferenceByUrl = new Dictionary<string, GitHubWorkItemReference>(StringComparer.OrdinalIgnoreCase);
        var latestReferenceByWorkItem = new Dictionary<int, GitHubWorkItemReference>();

        foreach (var reference in references)
        {
            var normalizedReferenceUrl = NormalizePullRequestUrl(reference.PullRequestUrl);
            if (normalizedReferenceUrl is null)
                continue;

            var normalizedReference = reference with { PullRequestUrl = normalizedReferenceUrl };
            if (!latestReferenceByUrl.TryGetValue(normalizedReferenceUrl, out var existingByUrl) ||
                IsReferenceBetter(normalizedReference, existingByUrl))
            {
                latestReferenceByUrl[normalizedReferenceUrl] = normalizedReference;
            }

            if (!latestReferenceByWorkItem.TryGetValue(normalizedReference.WorkItemNumber, out var existingByWorkItem) ||
                IsReferenceBetter(normalizedReference, existingByWorkItem))
            {
                latestReferenceByWorkItem[normalizedReference.WorkItemNumber] = normalizedReference;
            }
        }

        var lifecycleByUrl = new Dictionary<string, GitHubPullRequestLifecycle?>(StringComparer.OrdinalIgnoreCase);
        foreach (var workItem in byWorkItemNumber.Values.ToList())
        {
            string? observedUrl = null;
            string? observedState = null;
            string? targetState = null;

            if (latestReferenceByWorkItem.TryGetValue(workItem.WorkItemNumber, out var selectedReference))
            {
                observedUrl = NormalizePullRequestUrl(selectedReference.PullRequestUrl);
                observedState = ResolveObservedPullRequestState(selectedReference);
                targetState = ResolveStateFromReference(workItem, selectedReference);
            }
            else
            {
                var normalizedLinkedPrUrl = NormalizePullRequestUrl(workItem.LinkedPullRequestUrl);
                if (normalizedLinkedPrUrl is null)
                    continue;

                if (latestReferenceByUrl.TryGetValue(normalizedLinkedPrUrl, out var linkedReference))
                {
                    observedUrl = NormalizePullRequestUrl(linkedReference.PullRequestUrl);
                    observedState = ResolveObservedPullRequestState(linkedReference);
                    targetState = ResolveStateFromReference(workItem, linkedReference);
                }
                else
                {
                    if (!lifecycleByUrl.TryGetValue(normalizedLinkedPrUrl, out var lifecycle))
                    {
                        lifecycle = await gitHubApiService.GetPullRequestLifecycleByUrlAsync(userId, normalizedLinkedPrUrl);
                        lifecycleByUrl[normalizedLinkedPrUrl] = lifecycle;
                    }

                    if (lifecycle is null)
                        continue;

                    observedUrl = NormalizePullRequestUrl(lifecycle.PullRequestUrl) ?? normalizedLinkedPrUrl;
                    observedState = ResolveObservedPullRequestState(lifecycle);
                    targetState = ResolveStateFromLifecycle(workItem, lifecycle);
                }
            }

            if (observedState is null || targetState is null)
                continue;

            var normalizedObservedState = NormalizeObservedPullRequestState(workItem.LastObservedPullRequestState);
            var normalizedObservedUrl = NormalizePullRequestUrl(workItem.LastObservedPullRequestUrl);
            var observedStateChanged = !string.Equals(normalizedObservedState, observedState, StringComparison.OrdinalIgnoreCase);
            var observedUrlChanged = !string.Equals(normalizedObservedUrl, observedUrl, StringComparison.Ordinal);
            var lifecycleChanged = observedStateChanged || observedUrlChanged;

            // Respect manual overrides: only auto-change item status when PR lifecycle changed.
            var stateChanged = lifecycleChanged &&
                               !string.Equals(workItem.State, targetState, StringComparison.Ordinal);
            var linkChanged = !string.Equals(workItem.LinkedPullRequestUrl, observedUrl, StringComparison.Ordinal);
            if (!stateChanged && !linkChanged && !lifecycleChanged)
                continue;

            var persistedObservedState = lifecycleChanged ? observedState : null;
            var persistedObservedUrl = lifecycleChanged ? observedUrl : null;

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
                    LinkedPullRequestUrl: linkChanged ? observedUrl ?? string.Empty : null,
                    LastObservedPullRequestState: persistedObservedState,
                    LastObservedPullRequestUrl: persistedObservedUrl));

            byWorkItemNumber[workItem.WorkItemNumber] = workItem with
            {
                State = stateChanged ? targetState : workItem.State,
                LinkedPullRequestUrl = linkChanged ? observedUrl : workItem.LinkedPullRequestUrl,
                LastObservedPullRequestState = lifecycleChanged ? observedState : workItem.LastObservedPullRequestState,
                LastObservedPullRequestUrl = lifecycleChanged ? observedUrl : workItem.LastObservedPullRequestUrl,
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

    private static int GetPullRequestLifecycleRank(GitHubWorkItemReference reference)
    {
        if (reference.IsMerged)
            return 4;

        if (reference.IsOpen && !reference.IsDraft)
            return 3;

        if (reference.IsOpen && reference.IsDraft)
            return 2;

        return 1;
    }

    private static bool IsReferenceBetter(GitHubWorkItemReference candidate, GitHubWorkItemReference current)
    {
        var candidateRank = GetPullRequestLifecycleRank(candidate);
        var currentRank = GetPullRequestLifecycleRank(current);
        if (candidateRank != currentRank)
            return candidateRank > currentRank;

        if (candidate.IsFixReference != current.IsFixReference)
            return candidate.IsFixReference;

        return candidate.UpdatedAt > current.UpdatedAt;
    }

    private static string ResolveObservedPullRequestState(GitHubWorkItemReference reference)
    {
        if (reference.IsMerged) return "merged";
        if (reference.IsOpen && reference.IsDraft) return "draft";
        if (reference.IsOpen) return "open";
        return "closed";
    }

    private static string ResolveObservedPullRequestState(GitHubPullRequestLifecycle lifecycle)
    {
        if (lifecycle.IsMerged) return "merged";
        if (lifecycle.IsOpen && lifecycle.IsDraft) return "draft";
        if (lifecycle.IsOpen) return "open";
        return "closed";
    }

    private static string ResolveStateFromReference(WorkItemDto workItem, GitHubWorkItemReference reference)
    {
        // User-requested mapping:
        // draft -> in-progress, open -> in-pr, closed -> new, merged -> resolved
        if (reference.IsMerged)
            return workItem.IsAI ? "Resolved (AI)" : "Resolved";

        if (reference.IsOpen && !reference.IsDraft)
            return workItem.IsAI ? "In-PR (AI)" : "In-PR";

        if (reference.IsOpen && reference.IsDraft)
            return workItem.IsAI ? "In Progress (AI)" : "In Progress";

        return "New";
    }

    private static string ResolveStateFromLifecycle(WorkItemDto workItem, GitHubPullRequestLifecycle lifecycle)
    {
        if (lifecycle.IsMerged)
            return workItem.IsAI ? "Resolved (AI)" : "Resolved";

        if (lifecycle.IsOpen && !lifecycle.IsDraft)
            return workItem.IsAI ? "In-PR (AI)" : "In-PR";

        if (lifecycle.IsOpen && lifecycle.IsDraft)
            return workItem.IsAI ? "In Progress (AI)" : "In Progress";

        return "New";
    }

    private static string? NormalizePullRequestUrl(string? pullRequestUrl)
    {
        if (string.IsNullOrWhiteSpace(pullRequestUrl))
            return null;

        return pullRequestUrl.Trim().TrimEnd('/');
    }

    private static string? NormalizeObservedPullRequestState(string? observedPullRequestState)
    {
        if (string.IsNullOrWhiteSpace(observedPullRequestState))
            return null;

        return observedPullRequestState.Trim().ToLowerInvariant();
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
        public Task<LinkedAccount?> GetPrimaryByProviderAsync(int userId, string provider) => Task.FromResult<LinkedAccount?>(new LinkedAccount
        {
            Provider = provider,
            AccessToken = "test-token",
            UserProfileId = userId,
            IsPrimary = true,
        });
        public Task<IReadOnlyList<LinkedAccount>> GetByProviderAllAsync(int userId, string provider) =>
            Task.FromResult<IReadOnlyList<LinkedAccount>>(
            [
                new LinkedAccount
                {
                    Id = 1,
                    Provider = provider,
                    AccessToken = "test-token",
                    UserProfileId = userId,
                    ConnectedAt = DateTime.UtcNow,
                    IsPrimary = true,
                }
            ]);
        public Task<LinkedAccount?> GetByIdAsync(int userId, int accountId) =>
            Task.FromResult<LinkedAccount?>(new LinkedAccount
            {
                Id = accountId,
                Provider = "GitHub",
                AccessToken = "test-token",
                UserProfileId = userId,
                ConnectedAt = DateTime.UtcNow,
                IsPrimary = true,
            });
        public Task<IReadOnlyList<LinkedAccountDto>> GetAllAsync(int userId) =>
            Task.FromResult<IReadOnlyList<LinkedAccountDto>>(
            [
                new LinkedAccountDto(1, "GitHub", "test-user", "1", DateTime.UtcNow, true)
            ]);
        public Task<LinkedAccount> CreateAsync(LinkedAccount account) => Task.FromResult(account);
        public Task UpdateAsync(LinkedAccount account) => Task.CompletedTask;
        public Task DeleteAsync(LinkedAccount account) => Task.CompletedTask;
    }
}
