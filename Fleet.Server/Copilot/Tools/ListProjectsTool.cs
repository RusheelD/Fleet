using System.Text.Json;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists the user's projects (or just the active project in project-scoped chat).</summary>
public class ListProjectsTool(IProjectService projectService) : IChatTool
{
    public string Name => "list_projects";

    public string Description =>
        "List projects available to the current user. In project-scoped chat it returns only the active project.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
            projects = projects.Where(p => p.Id == context.ProjectId).ToList();

        if (projects.Count == 0)
            return "No projects found.";

        var result = projects.Select(project => new
        {
            project.Id,
            project.Slug,
            project.Title,
            project.Description,
            project.Repo,
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
        });

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
