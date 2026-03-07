using System.Text.Json;
using Fleet.Server.Projects;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Reads multiple work items in a single tool call.</summary>
public class BulkReadWorkItemsTool(
    IWorkItemService workItemService,
    IProjectService projectService) : IChatTool
{
    public string Name => "bulk_read_work_items";

    public string Description =>
        "Read multiple work items by their project-scoped numbers in one call. " +
        "In global chat provide projectId/projectSlug.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "ids": {
                    "type": "array",
                    "description": "Array of work-item numbers to read.",
                    "items": { "type": "integer" }
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
            "required": ["ids"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        if (args.Ids.Count == 0)
            return "Error: 'ids' array is required.";

        var project = await ResolveProjectAsync(context, args.ProjectId, args.ProjectSlug);
        if (project is null)
            return context.IsProjectScoped
                ? "Error: active project not found."
                : "Error: provide a valid 'projectId' or 'projectSlug' in global chat.";

        var results = new List<object>();
        foreach (var id in args.Ids)
        {
            if (id <= 0)
            {
                results.Add(new { Id = id, Error = "Invalid id." });
                continue;
            }

            try
            {
                var item = await workItemService.GetByWorkItemNumberAsync(project.Id, id);
                if (item is null)
                {
                    results.Add(new { Id = id, Error = "Not found." });
                    continue;
                }

                results.Add(new
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
                });
            }
            catch (Exception ex)
            {
                results.Add(new { Id = id, Error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new { Count = results.Count, Results = results },
            new JsonSerializerOptions { WriteIndented = true });
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

    private static BulkReadArgs ParseArgs(string argumentsJson)
    {
        try
        {
            var root = JsonDocument.Parse(argumentsJson).RootElement;
            var ids = new List<int>();
            if (root.TryGetProperty("ids", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var idEl in idsEl.EnumerateArray())
                {
                    if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var n))
                    {
                        ids.Add(n);
                        continue;
                    }

                    if (idEl.ValueKind == JsonValueKind.String && int.TryParse(idEl.GetString(), out var s))
                        ids.Add(s);
                }
            }

            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            return new BulkReadArgs(ids, projectId, projectSlug);
        }
        catch
        {
            return new BulkReadArgs([], null, null);
        }
    }

    private sealed record BulkReadArgs(IReadOnlyList<int> Ids, string? ProjectId, string? ProjectSlug);
}
