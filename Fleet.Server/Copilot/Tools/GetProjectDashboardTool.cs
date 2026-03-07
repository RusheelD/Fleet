using System.Text.Json;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Returns project dashboard metrics, activities, and agents.</summary>
public class GetProjectDashboardTool(IProjectService projectService) : IChatTool
{
    public string Name => "get_project_dashboard";

    public string Description =>
        "Get dashboard data for a project including metrics, recent activity, and agent status. " +
        "In project-scoped chat it is locked to the active project.";

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
        var project = await ChatToolProjectResolver.ResolveSingleProjectAsync(
            projectService,
            context,
            args.ProjectId,
            args.ProjectSlug,
            requireSelectorInGlobalScope: true);
        if (project is null)
            return "Error: provide a valid 'projectId' or 'projectSlug' in global chat.";

        var dashboard = await projectService.GetDashboardAsync(project.Id);
        if (dashboard is null)
            return "Project dashboard not found.";

        return JsonSerializer.Serialize(dashboard, new JsonSerializerOptions { WriteIndented = true });
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
