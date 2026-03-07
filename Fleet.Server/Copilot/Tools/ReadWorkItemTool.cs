using System.Text.Json;
using Fleet.Server.Projects;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Reads a single work item by its project-scoped number.</summary>
public class ReadWorkItemTool(
    IWorkItemService workItemService,
    IProjectService projectService) : IChatTool
{
    public string Name => "read_work_item";

    public string Description =>
        "Read a single work item by work-item number. In project-scoped chat it reads from the active project. " +
        "In global chat provide projectId or projectSlug.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "id": {
                    "type": "integer",
                    "description": "Work-item number to read."
                },
                "projectId": {
                    "type": "string",
                    "description": "Project id (required in global chat unless projectSlug is provided)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Project slug (alternative to projectId in global chat)."
                }
            },
            "required": ["id"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        if (args.Id <= 0)
            return "Error: 'id' (work-item number) is required.";

        var project = await ResolveProjectAsync(context, args.ProjectId, args.ProjectSlug);
        if (project is null)
            return context.IsProjectScoped
                ? "Error: active project not found."
                : "Error: provide a valid 'projectId' or 'projectSlug' in global chat.";

        var item = await workItemService.GetByWorkItemNumberAsync(project.Id, args.Id);
        if (item is null)
            return $"Error: work item #{args.Id} not found in project '{project.Slug}'.";

        return JsonSerializer.Serialize(new
        {
            ProjectId = project.Id,
            ProjectSlug = project.Slug,
            ProjectTitle = project.Title,
            Id = item.WorkItemNumber,
            item.Title,
            item.State,
            item.Priority,
            item.Difficulty,
            item.AssignedTo,
            item.Description,
            item.Tags,
            item.IsAI,
            ParentId = item.ParentWorkItemNumber,
            ChildIds = item.ChildWorkItemNumbers,
            item.LevelId,
            item.LinkedPullRequestUrl,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<Fleet.Server.Models.ProjectDto?> ResolveProjectAsync(
        ChatToolContext context,
        string? projectId,
        string? projectSlug)
    {
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
            return projects.FirstOrDefault(project => project.Id == context.ProjectId);

        if (!string.IsNullOrWhiteSpace(projectId))
            return projects.FirstOrDefault(project => project.Id == projectId);

        if (!string.IsNullOrWhiteSpace(projectSlug))
            return projects.FirstOrDefault(project => project.Slug.Equals(projectSlug, StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private static ReadWorkItemArgs ParseArgs(string argumentsJson)
    {
        try
        {
            var args = JsonDocument.Parse(argumentsJson).RootElement;
            var id = UpdateWorkItemTool.GetInt(args, "id") ?? 0;
            var projectId = args.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = args.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            return new ReadWorkItemArgs(id, projectId, projectSlug);
        }
        catch
        {
            return new ReadWorkItemArgs(0, null, null);
        }
    }

    private sealed record ReadWorkItemArgs(int Id, string? ProjectId, string? ProjectSlug);
}
