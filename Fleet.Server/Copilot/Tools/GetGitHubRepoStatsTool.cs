using System.Text.Json;
using Fleet.Server.GitHub;
using Fleet.Server.Models;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Returns GitHub repository statistics.</summary>
public class GetGitHubRepoStatsTool(
    IProjectService projectService,
    IGitHubApiService gitHubApiService) : IChatTool
{
    public string Name => "get_github_repo_stats";

    public string Description =>
        "Get GitHub stats (open PRs, merged PRs, recent commits, recent events) for a repository. " +
        "In project-scoped chat it is locked to the active project's repository.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "repo": {
                    "type": "string",
                    "description": "GitHub repo full name in owner/repo format (global chat only)."
                },
                "projectId": {
                    "type": "string",
                    "description": "Project id to resolve repository from (global chat only)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Project slug to resolve repository from (global chat only)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(context.UserId, out var userId))
            return "Error: invalid user ID.";

        var args = ParseArgs(argumentsJson);
        var repo = await ResolveRepoAsync(context, args.Repo, args.ProjectId, args.ProjectSlug);
        if (repo is null)
        {
            if (context.IsProjectScoped && !string.IsNullOrWhiteSpace(args.Repo))
                return "Project-scoped chat can only access the active project's repository.";

            return context.IsProjectScoped
                ? "This project does not have a connected GitHub repository."
                : "Error: provide 'repo', 'projectId', or 'projectSlug' in global chat.";
        }

        var stats = await gitHubApiService.GetRepoStatsAsync(userId, repo);
        var result = new
        {
            Repo = repo,
            stats.OpenPullRequests,
            stats.MergedPullRequests,
            stats.RecentCommits,
            RecentEvents = stats.RecentEvents.Select(evt => new
            {
                evt.Icon,
                evt.Text,
                evt.Timestamp,
            }),
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string?> ResolveRepoAsync(ChatToolContext context, string? repoArg, string? projectIdArg, string? projectSlugArg)
    {
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
        {
            var activeProject = projects.FirstOrDefault(project => project.Id == context.ProjectId);
            var activeRepo = activeProject?.Repo;
            if (string.IsNullOrWhiteSpace(activeRepo))
                return null;

            if (!string.IsNullOrWhiteSpace(repoArg) &&
                !string.Equals(activeRepo, repoArg, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return activeRepo;
        }

        if (!string.IsNullOrWhiteSpace(repoArg))
            return repoArg;

        var selectedProject = ResolveProject(projects, projectIdArg, projectSlugArg);
        return string.IsNullOrWhiteSpace(selectedProject?.Repo) ? null : selectedProject.Repo;
    }

    private static ProjectDto? ResolveProject(
        IReadOnlyList<ProjectDto> projects,
        string? projectId,
        string? projectSlug)
    {
        if (!string.IsNullOrWhiteSpace(projectId))
            return projects.FirstOrDefault(project => project.Id == projectId);

        if (!string.IsNullOrWhiteSpace(projectSlug))
            return projects.FirstOrDefault(project => project.Slug.Equals(projectSlug, StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private static RepoSelectorArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var repo = root.TryGetProperty("repo", out var repoEl) ? repoEl.GetString() : null;
            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            return new RepoSelectorArgs(repo, projectId, projectSlug);
        }
        catch
        {
            return new RepoSelectorArgs(null, null, null);
        }
    }

    private sealed record RepoSelectorArgs(string? Repo, string? ProjectId, string? ProjectSlug);
}
