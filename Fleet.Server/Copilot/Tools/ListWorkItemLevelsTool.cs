using System.Text.Json;
using Fleet.Server.Projects;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists work-item levels for one or more projects.</summary>
public class ListWorkItemLevelsTool(
    IProjectService projectService,
    IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "list_work_item_levels";

    public string Description =>
        "List work-item levels (type hierarchy) for projects. In project-scoped chat, only the active project is returned.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "projectId": {
                    "type": "string",
                    "description": "Optional project id filter (global chat only)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Optional project slug filter (global chat only)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        var projects = await ChatToolProjectResolver.ResolveProjectsAsync(
            projectService,
            context,
            args.ProjectId,
            args.ProjectSlug);
        if (projects.Count == 0)
            return "No projects found for the requested scope.";

        var result = new List<object>();
        foreach (var project in projects)
        {
            var levels = await workItemLevelService.GetByProjectIdAsync(project.Id);
            result.Add(new
            {
                ProjectId = project.Id,
                ProjectSlug = project.Slug,
                ProjectTitle = project.Title,
                Levels = levels.Select(level => new
                {
                    level.Id,
                    level.Name,
                    level.IconName,
                    level.Color,
                    level.Ordinal,
                    level.IsDefault,
                }),
            });
        }

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ProjectSelectorArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            return new ProjectSelectorArgs(projectId, projectSlug);
        }
        catch
        {
            return new ProjectSelectorArgs(null, null);
        }
    }

    private sealed record ProjectSelectorArgs(string? ProjectId, string? ProjectSlug);
}
