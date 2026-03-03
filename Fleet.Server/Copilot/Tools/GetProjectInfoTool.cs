using System.Text.Json;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Returns a compact summary of the current project.</summary>
public class GetProjectInfoTool(IProjectService projectService) : IChatTool
{
    public string Name => "get_project_info";

    public string Description =>
        "Get a summary of the current project including title, description, repository, work item counts, and agent status.";

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
        var project = projects.FirstOrDefault(p => p.Id == context.ProjectId);
        if (project is null)
            return "Project not found.";

        var result = new
        {
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
}
