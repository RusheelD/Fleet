using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Tries to update a work item; creates it if it does not exist.</summary>
public class TryUpdateWorkItemTool(IWorkItemService workItemService, IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "try_update_work_item";

    public bool IsWriteTool => true;

    public string Description =>
        "Update a work item by its project-scoped number. If the work item does not exist, " +
        "a brand-new work item is created with a new auto-assigned number (the id you supplied is ignored for creation). " +
        "Supply all the fields you want set — for updates only changed fields are applied; for creation, title is required.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "id": {
                    "type": "integer",
                    "description": "Work-item number to update. If not found, a new item is created instead."
                },
                "title": { "type": "string", "description": "Title (required for creation if item does not exist)." },
                "description": { "type": "string", "description": "Description." },
                "priority": {
                    "type": "string",
                    "description": "Priority: 1-4.",
                    "enum": ["1", "2", "3", "4"]
                },
                "difficulty": {
                    "type": "string",
                    "description": "Difficulty: 1-5.",
                    "enum": ["1", "2", "3", "4", "5"]
                },
                "state": {
                    "type": "string",
                    "description": "State.",
                    "enum": ["New", "Active", "Planning (AI)", "In Progress", "In Progress (AI)", "In-PR", "In-PR (AI)", "Resolved", "Resolved (AI)", "Closed"]
                },
                "level": {
                    "type": "string",
                    "description": "Work item type/level.",
                    "enum": ["Domain", "Module", "Feature", "Component", "Bug", "Task"]
                },
                "parent_id": {
                    "type": "integer",
                    "description": "Parent work-item number. Set to 0 to clear."
                },
                "tags": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Tags list."
                }
            },
            "required": ["id", "title"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = JsonDocument.Parse(argumentsJson).RootElement;
        var id = UpdateWorkItemTool.GetInt(args, "id") ?? 0;

        // Resolve level name → ID
        int? levelId = null;
        var levelName = UpdateWorkItemTool.GetString(args, "level");
        if (!string.IsNullOrWhiteSpace(levelName))
        {
            var levels = await workItemLevelService.GetByProjectIdAsync(context.ProjectId);
            levelId = levels.FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        // Try update first
        if (id > 0)
        {
            var updateReq = new Models.UpdateWorkItemRequest(
                Title: UpdateWorkItemTool.GetString(args, "title"),
                Description: UpdateWorkItemTool.GetString(args, "description"),
                Priority: UpdateWorkItemTool.GetInt(args, "priority"),
                Difficulty: UpdateWorkItemTool.GetInt(args, "difficulty"),
                State: UpdateWorkItemTool.GetString(args, "state"),
                AssignedTo: null,
                Tags: UpdateWorkItemTool.GetStringArray(args, "tags"),
                IsAI: null,
                ParentWorkItemNumber: UpdateWorkItemTool.GetInt(args, "parent_id"),
                LevelId: levelId
            );

            var updated = await workItemService.UpdateAsync(context.ProjectId, id, updateReq);
            if (updated is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    Action = "updated",
                    Id = updated.WorkItemNumber,
                    updated.Title,
                    updated.State,
                    updated.Priority,
                    updated.Difficulty,
                    updated.Description,
                    updated.Tags,
                    updated.ParentWorkItemNumber,
                    updated.LevelId,
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        // Item not found — create instead
        var title = UpdateWorkItemTool.GetString(args, "title") ?? "Untitled";
        var createReq = new Models.CreateWorkItemRequest(
            Title: title,
            Description: UpdateWorkItemTool.GetString(args, "description") ?? "",
            Priority: UpdateWorkItemTool.GetInt(args, "priority") ?? 3,
            Difficulty: UpdateWorkItemTool.GetInt(args, "difficulty") ?? 3,
            State: UpdateWorkItemTool.GetString(args, "state") ?? "New",
            AssignedTo: "Unassigned",
            Tags: UpdateWorkItemTool.GetStringArray(args, "tags") ?? [],
            IsAI: false,
            ParentWorkItemNumber: UpdateWorkItemTool.GetInt(args, "parent_id"),
            LevelId: levelId
        );

        var created = await workItemService.CreateAsync(context.ProjectId, createReq);

        return JsonSerializer.Serialize(new
        {
            Action = "created",
            Id = created.WorkItemNumber,
            created.Title,
            created.State,
            created.Priority,
            created.Difficulty,
            created.Description,
            created.Tags,
            ParentId = created.ParentWorkItemNumber,
            created.LevelId,
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
