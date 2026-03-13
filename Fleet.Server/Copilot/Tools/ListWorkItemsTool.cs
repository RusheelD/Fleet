using System.Text.Json;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists work items in the active project or across user projects (global scope).</summary>
public class ListWorkItemsTool(
    IWorkItemService workItemService,
    IProjectService projectService,
    IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "list_work_items";

    public string Description =>
        "List work items. In project-scoped chat, returns only the active project's items. " +
        "In global chat, optionally filter by projectId/projectSlug, otherwise returns items across all projects.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "state": {
                    "type": "string",
                    "description": "Optional filter: only return work items with this state."
                },
                "projectId": {
                    "type": "string",
                    "description": "Optional project id filter (global chat only)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Optional project slug filter (global chat only)."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of work items to return (default 20)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        var projects = await ResolveProjectsAsync(context, args);
        if (projects.Count == 0)
            return "No projects found for the requested scope.";

        var records = new List<FlattenedWorkItem>();
        foreach (var project in projects)
        {
            var items = await workItemService.GetByProjectIdAsync(project.Id);
            var levels = await workItemLevelService.GetByProjectIdAsync(project.Id);
            var levelsById = levels.ToDictionary(level => level.Id);
            var itemsByNumber = items.ToDictionary(item => item.WorkItemNumber);

            records.AddRange(items.Select(item => new FlattenedWorkItem(project, item, levelsById, itemsByNumber)));
        }

        if (!string.IsNullOrWhiteSpace(args.State))
        {
            records = records
                .Where(record => record.Item.State.Equals(args.State, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var materialized = records
            .OrderByDescending(record => record.Item.WorkItemNumber)
            .Take(args.Limit)
            .Select(record => new
            {
                ProjectId = record.Project.Id,
                ProjectSlug = record.Project.Slug,
                ProjectTitle = record.Project.Title,
                Id = record.Item.WorkItemNumber,
                record.Item.Title,
                record.Item.State,
                record.Item.Priority,
                record.Item.Difficulty,
                record.Item.AssignedTo,
                record.Item.Tags,
                record.Item.IsAI,
                LevelId = record.Item.LevelId,
                LevelName = record.LevelName,
                Type = record.LevelName,
                Parent = record.Parent,
                Children = record.Children,
            })
            .ToList();

        return materialized.Count == 0
            ? "No work items found matching the criteria."
            : JsonSerializer.Serialize(materialized, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<List<ProjectDto>> ResolveProjectsAsync(ChatToolContext context, ListWorkItemsArgs args)
    {
        var projects = await projectService.GetAllProjectsAsync();

        if (context.IsProjectScoped)
            return projects.Where(project => project.Id == context.ProjectId).ToList();

        if (!string.IsNullOrWhiteSpace(args.ProjectId))
            return projects.Where(project => string.Equals(project.Id, args.ProjectId, StringComparison.Ordinal)).ToList();

        if (!string.IsNullOrWhiteSpace(args.ProjectSlug))
            return projects.Where(project => string.Equals(project.Slug, args.ProjectSlug, StringComparison.OrdinalIgnoreCase)).ToList();

        return projects.ToList();
    }

    private static ListWorkItemsArgs ParseArgs(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var state = root.TryGetProperty("state", out var stateEl) ? stateEl.GetString() : null;
            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var limitValue)
                ? limitValue
                : 20;

            return new ListWorkItemsArgs(state, projectId, projectSlug, Math.Clamp(limit, 1, 200));
        }
        catch
        {
            return new ListWorkItemsArgs(null, null, null, 20);
        }
    }

    private sealed record ListWorkItemsArgs(string? State, string? ProjectId, string? ProjectSlug, int Limit);

    private sealed record FlattenedWorkItem(
        ProjectDto Project,
        WorkItemDto Item,
        IReadOnlyDictionary<int, WorkItemLevelDto> LevelsById,
        IReadOnlyDictionary<int, WorkItemDto> ItemsByNumber)
    {
        public string? LevelName => Item.LevelId is int levelId && LevelsById.TryGetValue(levelId, out var level)
            ? level.Name
            : null;

        public object? Parent => Item.ParentWorkItemNumber is int parentNumber
            ? BuildReference(parentNumber)
            : null;

        public IReadOnlyList<object> Children => Item.ChildWorkItemNumbers
            .Select(BuildReference)
            .ToList();

        private object BuildReference(int workItemNumber)
        {
            return new
            {
                Id = workItemNumber,
                Title = ItemsByNumber.TryGetValue(workItemNumber, out var item) ? item.Title : (string?)null,
            };
        }
    }
}
