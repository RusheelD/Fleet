using System.Text.Json;
using Fleet.Server.GitHub;
using Fleet.Server.Models;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists Fleet work-item references found in GitHub pull requests.</summary>
public class ListGitHubWorkItemReferencesTool(
    IProjectService projectService,
    IGitHubApiService gitHubApiService) : IChatTool
{
    public string Name => "list_github_work_item_references";

    public string Description =>
        "List work-item references parsed from GitHub pull request text (e.g., F#123, fix F#123). " +
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
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of references to return (default 100)."
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

        var refs = await gitHubApiService.GetWorkItemReferencesAsync(userId, repo);
        var result = refs
            .Take(args.Limit)
            .Select(reference => new
            {
                Repo = repo,
                reference.WorkItemNumber,
                reference.PullRequestUrl,
                reference.PullRequestTitle,
                reference.IsFixReference,
                reference.IsMerged,
                reference.IsOpen,
                reference.IsDraft,
                reference.UpdatedAt,
            })
            .ToList();

        return result.Count == 0
            ? "No work-item references found in pull requests."
            : JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
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
            var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var limitValue)
                ? limitValue
                : 100;
            return new RepoSelectorArgs(repo, projectId, projectSlug, Math.Clamp(limit, 1, 300));
        }
        catch
        {
            return new RepoSelectorArgs(null, null, null, 100);
        }
    }

    private sealed record RepoSelectorArgs(string? Repo, string? ProjectId, string? ProjectSlug, int Limit);
}
