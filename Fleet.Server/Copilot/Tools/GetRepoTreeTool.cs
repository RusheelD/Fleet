using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fleet.Server.Connections;
using Fleet.Server.Models;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Returns the folder/file tree of the connected GitHub repository.
/// Uses the GitHub Git Trees API with recursive mode.
/// </summary>
public class GetRepoTreeTool(
    IProjectService projectService,
    IConnectionRepository connectionRepository,
    IGitHubTokenProtector tokenProtector,
    IHttpClientFactory httpClientFactory) : IChatTool
{
    public string Name => "get_repo_tree";

    public string Description =>
        "Get the file/folder structure of a GitHub repository. " +
        "In project-scoped chat it is locked to the active project's repository. " +
        "In global chat provide 'repo' or a project selector.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "repo": {
                    "type": "string",
                    "description": "GitHub repository full name in owner/repo format (global chat only)."
                },
                "projectId": {
                    "type": "string",
                    "description": "Project id to resolve repository from (global chat only)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Project slug to resolve repository from (global chat only)."
                },
                "path": {
                    "type": "string",
                    "description": "Optional subdirectory path to filter the tree (e.g. 'src/components'). Leave empty for the full tree."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var repoArg = args.TryGetProperty("repo", out var repoProp) ? repoProp.GetString() : null;
        var projectIdArg = args.TryGetProperty("projectId", out var projectIdProp) ? projectIdProp.GetString() : null;
        var projectSlugArg = args.TryGetProperty("projectSlug", out var projectSlugProp) ? projectSlugProp.GetString() : null;
        var pathFilter = args.TryGetProperty("path", out var pathProp) ? pathProp.GetString()?.Trim('/') : null;

        // Resolve the requested repo from scope + arguments.
        var repoFullName = await ResolveRepoFullNameAsync(context, repoArg, projectIdArg, projectSlugArg);
        if (repoFullName is null)
        {
            if (context.IsProjectScoped && !string.IsNullOrWhiteSpace(repoArg))
                return "Project-scoped chat can only access the active project's repository.";

            return context.IsProjectScoped
                ? "This project does not have a connected GitHub repository."
                : "Error: provide 'repo', 'projectId', or 'projectSlug' in global chat.";
        }

        // Get the user's GitHub token
        if (!int.TryParse(context.UserId, out var userId))
            return "Error: invalid user ID.";

        var account = await connectionRepository.GetByProviderAsync(userId, "GitHub");
        var accessToken = tokenProtector.Unprotect(account?.AccessToken);
        if (account is null || string.IsNullOrEmpty(accessToken))
            return "No GitHub account is linked. Please connect GitHub in Settings first.";

        var client = httpClientFactory.CreateClient("GitHub");

        // Fetch the default branch's tree recursively
        try
        {
            // 1. Get the default branch SHA
            var repoInfo = await FetchJsonAsync<RepoResponse>(client, accessToken,
                $"https://api.github.com/repos/{repoFullName}", cancellationToken);
            var defaultBranch = repoInfo?.DefaultBranch ?? "main";

            var branchInfo = await FetchJsonAsync<BranchResponse>(client, accessToken,
                $"https://api.github.com/repos/{repoFullName}/branches/{defaultBranch}", cancellationToken);
            var treeSha = branchInfo?.Commit?.Commit?.Tree?.Sha;
            if (treeSha is null)
                return "Could not resolve the repository's default branch tree.";

            // 2. Fetch the recursive tree
            var treeResponse = await FetchJsonAsync<TreeResponse>(client, accessToken,
                $"https://api.github.com/repos/{repoFullName}/git/trees/{treeSha}?recursive=1", cancellationToken);

            if (treeResponse?.Tree is null)
                return "Could not fetch repository tree.";

            var entries = treeResponse.Tree
                .Where(e => string.IsNullOrEmpty(pathFilter) ||
                            e.Path.StartsWith(pathFilter + "/", StringComparison.OrdinalIgnoreCase) ||
                            e.Path.Equals(pathFilter, StringComparison.OrdinalIgnoreCase))
                .Select(e => new
                {
                    e.Path,
                    Type = e.Type == "blob" ? "file" : "dir",
                    e.Size,
                })
                .Take(500) // Safety cap
                .ToList();

            if (entries.Count == 0)
                return $"No files found{(pathFilter is not null ? $" under '{pathFilter}'" : "")}.";

            var result = new
            {
                repo = repoFullName,
                branch = defaultBranch,
                filter = pathFilter,
                totalEntries = entries.Count,
                truncated = treeResponse.Truncated,
                entries,
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            return $"GitHub API error: {ex.Message}";
        }
    }

    private async Task<string?> ResolveRepoFullNameAsync(
        ChatToolContext context,
        string? repoArg,
        string? projectIdArg,
        string? projectSlugArg)
    {
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
        {
            var activeProject = projects.FirstOrDefault(p => p.Id == context.ProjectId);
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

    private static async Task<T?> FetchJsonAsync<T>(HttpClient client, string token, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    // ── GitHub API models ─────────────────────────────────────

    private sealed class RepoResponse
    {
        [JsonPropertyName("default_branch")]
        public string? DefaultBranch { get; set; }
    }

    private sealed class BranchResponse
    {
        [JsonPropertyName("commit")]
        public BranchCommit? Commit { get; set; }
    }

    private sealed class BranchCommit
    {
        [JsonPropertyName("commit")]
        public CommitDetail? Commit { get; set; }
    }

    private sealed class CommitDetail
    {
        [JsonPropertyName("tree")]
        public TreeRef? Tree { get; set; }
    }

    private sealed class TreeRef
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }

    private sealed class TreeResponse
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("tree")]
        public List<TreeEntry>? Tree { get; set; }

        [JsonPropertyName("truncated")]
        public bool Truncated { get; set; }
    }

    private sealed class TreeEntry
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }
}
