using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fleet.Server.Connections;
using Fleet.Server.Models;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Reads the content of a single file from the connected GitHub repository.
/// Uses the GitHub Contents API.
/// </summary>
public class ReadRepoFileTool(
    IProjectService projectService,
    IConnectionRepository connectionRepository,
    IGitHubTokenProtector tokenProtector,
    IHttpClientFactory httpClientFactory) : IChatTool
{
    /// <summary>Max file size we'll fetch (100 KB). Larger files are rejected to protect context.</summary>
    private const int MaxFileSizeBytes = 100_000;

    public string Name => "read_repo_file";

    public string Description =>
        "Read a specific file from a GitHub repository. In project-scoped chat it is locked to the active project's repository. " +
        "In global chat provide 'repo' or a project selector. " +
        "Provide the file path relative to the repository root (e.g. 'src/App.tsx'). " +
        "Only text files up to 100 KB are supported.";

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
                    "description": "File path relative to the repository root (e.g. 'README.md', 'src/main.tsx')."
                }
            },
            "required": ["path"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var repoArg = args.TryGetProperty("repo", out var repoProp) ? repoProp.GetString() : null;
        var projectIdArg = args.TryGetProperty("projectId", out var projectIdProp) ? projectIdProp.GetString() : null;
        var projectSlugArg = args.TryGetProperty("projectSlug", out var projectSlugProp) ? projectSlugProp.GetString() : null;
        if (!args.TryGetProperty("path", out var pathProp) || string.IsNullOrWhiteSpace(pathProp.GetString()))
            return "Error: 'path' parameter is required.";

        var filePath = pathProp.GetString()!.Trim('/');

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

        try
        {
            var url = $"https://api.github.com/repos/{repoFullName}/contents/{filePath}";
            var fileInfo = await FetchJsonAsync<ContentsResponse>(client, accessToken, url, cancellationToken);

            if (fileInfo is null)
                return $"File not found: {filePath}";

            if (fileInfo.Type != "file")
                return $"'{filePath}' is a {fileInfo.Type}, not a file. Use get_repo_tree to browse directories.";

            if (fileInfo.Size > MaxFileSizeBytes)
                return $"File is too large ({fileInfo.Size:N0} bytes). Maximum supported size is {MaxFileSizeBytes:N0} bytes.";

            // The Contents API returns base64-encoded content for files under 1 MB
            if (string.IsNullOrEmpty(fileInfo.Content))
                return $"Could not retrieve content for '{filePath}'. The file may be too large for the Contents API.";

            var contentBytes = Convert.FromBase64String(fileInfo.Content.Replace("\n", ""));
            var content = System.Text.Encoding.UTF8.GetString(contentBytes);

            var result = new
            {
                repo = repoFullName,
                path = filePath,
                size = fileInfo.Size,
                content,
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"File not found: {filePath}";
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

    // ── GitHub API model ──────────────────────────────────────

    private sealed class ContentsResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("encoding")]
        public string? Encoding { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }
}
