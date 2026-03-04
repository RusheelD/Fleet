using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Updates multiple work items in a single tool call.</summary>
public class BulkUpdateWorkItemsTool(IWorkItemService workItemService, IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "bulk_update_work_items";

    public bool IsWriteTool => true;

    public string Description =>
        "Update multiple work items in a single call. Each item needs its id (work-item number) and the fields to change. " +
        "Returns an array of results (updated items or errors). Use this instead of calling update_work_item in a loop.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "items": {
                    "type": "array",
                    "description": "Array of items to update.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "integer", "description": "Work-item number to update." },
                            "title": { "type": "string" },
                            "description": { "type": "string" },
                            "priority": { "type": "string", "enum": ["1", "2", "3", "4"] },
                            "difficulty": { "type": "string", "enum": ["1", "2", "3", "4", "5"] },
                            "state": { "type": "string", "enum": ["New", "Active", "In Progress", "Resolved", "Closed"] },
                            "level": { "type": "string", "enum": ["Domain", "Module", "Feature", "Component", "Bug", "Task"] },
                            "parent_id": { "type": "integer", "description": "Set to 0 to clear parent." },
                            "tags": { "type": "array", "items": { "type": "string" } }
                        },
                        "required": ["id"]
                    }
                }
            },
            "required": ["items"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var root = JsonDocument.Parse(argumentsJson).RootElement;
        if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
            return "Error: 'items' array is required.";

        var levels = await workItemLevelService.GetByProjectIdAsync(context.ProjectId);
        var results = new List<object>();

        foreach (var item in itemsEl.EnumerateArray())
        {
            var id = UpdateWorkItemTool.GetInt(item, "id") ?? 0;
            if (id <= 0) { results.Add(new { Error = "Missing or invalid 'id'." }); continue; }

            try
            {
                int? levelId = null;
                var levelName = UpdateWorkItemTool.GetString(item, "level");
                if (!string.IsNullOrWhiteSpace(levelName))
                    levelId = levels.FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))?.Id;

                var request = new Models.UpdateWorkItemRequest(
                    Title: UpdateWorkItemTool.GetString(item, "title"),
                    Description: UpdateWorkItemTool.GetString(item, "description"),
                    Priority: UpdateWorkItemTool.GetInt(item, "priority"),
                    Difficulty: UpdateWorkItemTool.GetInt(item, "difficulty"),
                    State: UpdateWorkItemTool.GetString(item, "state"),
                    AssignedTo: null,
                    Tags: UpdateWorkItemTool.GetStringArray(item, "tags"),
                    IsAI: null,
                    ParentWorkItemNumber: UpdateWorkItemTool.GetInt(item, "parent_id"),
                    LevelId: levelId
                );

                var updated = await workItemService.UpdateAsync(context.ProjectId, id, request);
                if (updated is null)
                {
                    results.Add(new { Id = id, Error = "Not found." });
                }
                else
                {
                    results.Add(new
                    {
                        Id = updated.WorkItemNumber,
                        updated.Title,
                        updated.State,
                        updated.Priority,
                        updated.Difficulty,
                        updated.LevelId,
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new { Id = id, Error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new { Updated = results.Count, Results = results },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
