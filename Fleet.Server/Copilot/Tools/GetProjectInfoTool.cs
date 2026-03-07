using System.Text.Json;
using Fleet.Server.Models;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Returns a compact summary of the current project.</summary>
public class GetProjectInfoTool(IProjectService projectService) : IChatTool
{
    public string Name => "get_project_info";

    public string Description =>
        "Get a summary of a project including title, description, repository, work item counts, and agent status. " +
        "In project-scoped chat, it is locked to the active project.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "projectId": {
                    "type": "string",
                    "description": "Project id (required in global chat unless projectSlug is provided)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Project slug (alternative to projectId in global chat)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
        {
            var project = projects.FirstOrDefault(p => p.Id == context.ProjectId);
            if (project is null)
                return "Project not found.";

            return SerializeProject(project);
        }

        if (string.IsNullOrWhiteSpace(args.ProjectId) && string.IsNullOrWhiteSpace(args.ProjectSlug))
            return "Error: provide 'projectId' or 'projectSlug' in global chat, or call list_projects first.";

        var selectedProject = projects.FirstOrDefault(project =>
            (!string.IsNullOrWhiteSpace(args.ProjectId) &&
             string.Equals(project.Id, args.ProjectId, StringComparison.Ordinal)) ||
            (!string.IsNullOrWhiteSpace(args.ProjectSlug) &&
             string.Equals(project.Slug, args.ProjectSlug, StringComparison.OrdinalIgnoreCase)));

        if (selectedProject is null)
            return "Project not found.";

        return SerializeProject(selectedProject);
    }

    private static string SerializeProject(ProjectDto project)
    {
        var result = new
        {
            project.Id,
            project.Title,
            project.Description,
            project.Repo,
            project.Slug,
            WorkItems = new
            {
                project.WorkItems.Total,
                project.WorkItems.Active,
                project.WorkItems.Resolved,
            },
            Agents = new
            {
                project.Agents.Total,
                project.Agents.Running,
            },
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ProjectInfoArgs ParseArgs(string argumentsJson)
    {
        try
        {
            var root = JsonDocument.Parse(argumentsJson).RootElement;
            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            return new ProjectInfoArgs(projectId, projectSlug);
        }
        catch
        {
            return new ProjectInfoArgs(null, null);
        }
    }

    private sealed record ProjectInfoArgs(string? ProjectId, string? ProjectSlug);
}
