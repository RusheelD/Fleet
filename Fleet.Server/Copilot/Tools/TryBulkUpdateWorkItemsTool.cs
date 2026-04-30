using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Tries to update each work item; creates any that do not exist.</summary>
public class TryBulkUpdateWorkItemsTool(IWorkItemService workItemService, IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "try_bulk_update_work_items";

    public bool IsWriteTool => true;

    public string Description =>
        "For each item in the array, tries to update the work item by its number. " +
        "If the work item does not exist, creates a new one instead (with a new auto-assigned number). " +
        "Returns an array showing whether each item was updated or created. " +
        "Use this instead of calling try_update_work_item in a loop. " +
        "parent_id can be an integer (existing work-item number) or a string like \"@2\" to reference " +
        "the item at index 2 in THIS batch (0-based). Items are processed in order, so only back-references work.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "items": {
                    "type": "array",
                    "description": "Array of items to update-or-create.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "integer", "description": "Work-item number to try updating. If not found, a new item is created." },
                            "title": { "type": "string", "description": "Title (required when creating)." },
                            "description": { "type": "string" },
                            "priority": { "type": "string", "enum": ["1", "2", "3", "4"] },
                            "difficulty": { "type": "string", "enum": ["1", "2", "3", "4", "5"] },
                            "state": { "type": "string", "enum": ["New", "Active", "Planning (AI)", "In Progress", "In Progress (AI)", "In-PR", "In-PR (AI)", "Resolved", "Resolved (AI)", "Closed"] },
                            "level": { "type": "string", "enum": ["Domain", "Module", "Feature", "Component", "Bug", "Task"] },
                            "parent_id": { "type": ["integer", "string"], "description": "Parent work-item number (integer), or batch index ref like '@2'. Set to 0 to clear." },
                            "tags": { "type": "array", "items": { "type": "string" } }
                        },
                        "required": ["id", "title"]
                    }
                }
            },
            "required": ["items"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!context.TryGetProjectId(out var projectId))
            return ChatToolContext.ProjectScopeRequiredMessage;

        var root = JsonDocument.Parse(argumentsJson).RootElement;
        if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
            return "Error: 'items' array is required.";

        var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
        var results = new List<object>();
        var createdNumbers = new List<int>(); // tracks WIT numbers by batch index for @N refs

        foreach (var item in itemsEl.EnumerateArray())
        {
            var id = UpdateWorkItemTool.GetInt(item, "id") ?? 0;

            try
            {
                int? levelId = null;
                var levelName = UpdateWorkItemTool.GetString(item, "level");
                if (!string.IsNullOrWhiteSpace(levelName))
                    levelId = levels.FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))?.Id;

                var parentId = UpdateWorkItemTool.ResolveParentId(item, createdNumbers);

                // Try update if we have a positive ID
                if (id > 0)
                {
                    var updateReq = new Models.UpdateWorkItemRequest(
                        Title: UpdateWorkItemTool.GetString(item, "title"),
                        Description: UpdateWorkItemTool.GetString(item, "description"),
                        Priority: UpdateWorkItemTool.GetInt(item, "priority"),
                        Difficulty: UpdateWorkItemTool.GetInt(item, "difficulty"),
                        State: UpdateWorkItemTool.GetString(item, "state"),
                        AssignedTo: null,
                        Tags: UpdateWorkItemTool.GetStringArray(item, "tags"),
                        IsAI: null,
                        ParentWorkItemNumber: parentId,
                        LevelId: levelId
                    );

                    var updated = await workItemService.UpdateAsync(projectId, id, updateReq);
                    if (updated is not null)
                    {
                        createdNumbers.Add(updated.WorkItemNumber);
                        results.Add(new
                        {
                            Action = "updated",
                            Index = createdNumbers.Count - 1,
                            Id = updated.WorkItemNumber,
                            updated.Title,
                            updated.State,
                            updated.Priority,
                            updated.Difficulty,
                            updated.LevelId,
                        });
                        continue;
                    }
                }

                // Not found or id <= 0 → create
                var title = UpdateWorkItemTool.GetString(item, "title") ?? "Untitled";
                var createReq = new Models.CreateWorkItemRequest(
                    Title: title,
                    Description: UpdateWorkItemTool.GetString(item, "description") ?? "",
                    Priority: UpdateWorkItemTool.GetInt(item, "priority") ?? 3,
                    Difficulty: UpdateWorkItemTool.GetInt(item, "difficulty") ?? 3,
                    State: UpdateWorkItemTool.GetString(item, "state") ?? "New",
                    AssignedTo: context.DefaultCreatedWorkItemAssignee,
                    Tags: UpdateWorkItemTool.GetStringArray(item, "tags") ?? [],
                    IsAI: context.DefaultCreatedWorkItemIsAi,
                    ParentWorkItemNumber: parentId,
                    LevelId: levelId,
                    AssignmentMode: context.DefaultCreatedWorkItemAssignmentMode
                );

                var created = await workItemService.CreateAsync(projectId, createReq);
                createdNumbers.Add(created.WorkItemNumber);
                results.Add(new
                {
                    Action = "created",
                    Index = createdNumbers.Count - 1,
                    Id = created.WorkItemNumber,
                    created.Title,
                    created.State,
                    created.Priority,
                    created.Difficulty,
                    created.LevelId,
                });
            }
            catch (Exception ex)
            {
                createdNumbers.Add(0); // placeholder so subsequent @N indices stay correct
                results.Add(new { Id = id, Error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new { Processed = results.Count, Results = results },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
