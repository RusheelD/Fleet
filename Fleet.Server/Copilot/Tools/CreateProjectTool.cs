using System.Text.Json;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Creates a new project linked to a selected GitHub repository.</summary>
public class CreateProjectTool(IProjectService projectService) : IChatTool
{
    public string Name => "create_project";

    public string Description =>
        "Create a new project and link it to a GitHub repository (owner/repo). " +
        "Only available in global chat scope when no project is open.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "title": {
                    "type": "string",
                    "description": "Project title."
                },
                "description": {
                    "type": "string",
                    "description": "Optional project description."
                },
                "repo": {
                    "type": "string",
                    "description": "GitHub repository in owner/repo format."
                },
                "branchPattern": {
                    "type": "string",
                    "description": "Optional project branch naming pattern."
                },
                "commitAuthorMode": {
                    "type": "string",
                    "description": "Optional commit author mode (fleet or custom)."
                },
                "commitAuthorName": {
                    "type": "string",
                    "description": "Optional custom commit author name."
                },
                "commitAuthorEmail": {
                    "type": "string",
                    "description": "Optional custom commit author email."
                }
            },
            "required": ["title", "repo"]
        }
        """;

    public bool IsWriteTool => true;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsProjectScoped)
            return "This tool is only available in global chat when no project is open.";

        var args = ParseArgs(argumentsJson);
        if (string.IsNullOrWhiteSpace(args.Title))
            return "Error: 'title' is required.";

        if (string.IsNullOrWhiteSpace(args.Repo))
            return "Error: 'repo' is required and must be in owner/repo format.";

        try
        {
            var project = await projectService.CreateProjectAsync(
                args.Title.Trim(),
                args.Description?.Trim() ?? string.Empty,
                args.Repo.Trim(),
                args.BranchPattern?.Trim(),
                args.CommitAuthorMode?.Trim(),
                args.CommitAuthorName?.Trim(),
                args.CommitAuthorEmail?.Trim());

            var result = new
            {
                message = "Project created successfully.",
                project.Id,
                project.Slug,
                project.Title,
                project.Description,
                project.Repo,
                project.BranchPattern,
                project.CommitAuthorMode,
                project.CommitAuthorName,
                project.CommitAuthorEmail,
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (InvalidOperationException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static CreateProjectArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
            var description = root.TryGetProperty("description", out var descriptionEl) ? descriptionEl.GetString() : null;
            var repo = root.TryGetProperty("repo", out var repoEl) ? repoEl.GetString() : null;
            var branchPattern = root.TryGetProperty("branchPattern", out var branchPatternEl) ? branchPatternEl.GetString() : null;
            var commitAuthorMode = root.TryGetProperty("commitAuthorMode", out var commitAuthorModeEl) ? commitAuthorModeEl.GetString() : null;
            var commitAuthorName = root.TryGetProperty("commitAuthorName", out var commitAuthorNameEl) ? commitAuthorNameEl.GetString() : null;
            var commitAuthorEmail = root.TryGetProperty("commitAuthorEmail", out var commitAuthorEmailEl) ? commitAuthorEmailEl.GetString() : null;
            return new CreateProjectArgs(
                title,
                description,
                repo,
                branchPattern,
                commitAuthorMode,
                commitAuthorName,
                commitAuthorEmail);
        }
        catch
        {
            return new CreateProjectArgs(null, null, null, null, null, null, null);
        }
    }

    private sealed record CreateProjectArgs(
        string? Title,
        string? Description,
        string? Repo,
        string? BranchPattern,
        string? CommitAuthorMode,
        string? CommitAuthorName,
        string? CommitAuthorEmail);
}
