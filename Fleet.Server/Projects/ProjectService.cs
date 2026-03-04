using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.GitHub;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Projects;

public class ProjectService(
    IProjectRepository projectRepository,
    IWorkItemRepository workItemRepository,
    IAgentTaskRepository agentTaskRepository,
    IGitHubApiService gitHubApiService,
    IAuthService authService,
    ILogger<ProjectService> logger) : IProjectService
{
    private async Task<string> GetCurrentOwnerIdAsync() =>
        (await authService.GetCurrentUserIdAsync()).ToString();

    public async Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync()
    {
        logger.ProjectsRetrievingAll();
        var projects = await projectRepository.GetAllAsync();
        var summaries = await workItemRepository.GetSummariesByProjectAsync();

        return projects.Select(p =>
        {
            var wi = summaries.GetValueOrDefault(p.Id, new WorkItemSummaryDto(0, 0, 0));
            return p with { WorkItems = wi };
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

        var available = await projectRepository.IsSlugAvailableAsync(slug);
        logger.ProjectsSlugAvailability(slug.SanitizeForLogging(), available);
        return new SlugCheckResult(slug, available);
    }

    public async Task<ProjectDto> CreateProjectAsync(string title, string description, string repo)
    {
        logger.ProjectsCreating(title.SanitizeForLogging());
        var ownerId = await GetCurrentOwnerIdAsync();
        return await projectRepository.CreateAsync(ownerId, title, description, repo);
    }

    public async Task<ProjectDto?> UpdateProjectAsync(string id, string? title, string? description, string? repo)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = id
        });

        logger.ProjectsUpdating(id.SanitizeForLogging());
        return await projectRepository.UpdateAsync(id, title, description, repo);
    }

    public async Task<bool> DeleteProjectAsync(string id)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = id
        });

        logger.ProjectsDeleting(id.SanitizeForLogging());
        return await projectRepository.DeleteAsync(id);
    }

    public async Task<ProjectDashboardDto?> GetDashboardBySlugAsync(string slug)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectSlug"] = slug
        });

        logger.ProjectsDashboardBySlug(slug.SanitizeForLogging());
        var project = await projectRepository.GetBySlugAsync(slug);
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
        var project = await projectRepository.GetByIdAsync(projectId);
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

        // ── Real work item metrics from the database ──────────────
        var workItems = await workItemRepository.GetByProjectIdAsync(project.Id);
        var totalItems = workItems.Count;
        var activeItems = workItems.Count(w =>
            w.State.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
            w.State.Equals("New", StringComparison.OrdinalIgnoreCase));
        var resolvedItems = workItems.Count(w =>
            w.State.Equals("Resolved", StringComparison.OrdinalIgnoreCase));
        var closedItems = workItems.Count(w =>
            w.State.Equals("Closed", StringComparison.OrdinalIgnoreCase));
        var completedItems = resolvedItems + closedItems;
        var completionPct = totalItems > 0 ? Math.Round((double)completedItems / totalItems, 2) : 0;

        // ── Real GitHub stats ─────────────────────────────────────
        var userId = await authService.GetCurrentUserIdAsync();
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
            new("bot", "Active Agents", project.Agents.Running.ToString(),
                $"of {project.Agents.Total} allocated", null),
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
}
