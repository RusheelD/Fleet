using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Creates multiple work items in a single tool call to avoid rate-limit issues.</summary>
public class BulkCreateWorkItemsTool(IWorkItemService workItemService, IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "bulk_create_work_items";

    public bool IsWriteTool => true;

    public string Description =>
        "Create multiple work items in a single call. Accepts an array of items. " +
        "Each item has the same fields as create_work_item. Returns an array of results (created items or errors). " +
        "Use this instead of calling create_work_item in a loop to avoid rate-limit (429) errors. " +
        "parent_id can be an integer (existing work-item number) or a string like \"@2\" to reference " +
        "the item at index 2 in THIS batch (0-based). Items are processed in order, so only back-references work.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "items": {
                    "type": "array",
                    "description": "Array of work items to create.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "title": { "type": "string", "description": "Title of the work item." },
                            "description": { "type": "string", "description": "Detailed description." },
                            "priority": {
                                "type": "string",
                                "description": "Priority: 1-4. Default 3.",
                                "enum": ["1", "2", "3", "4"]
                            },
                            "difficulty": {
                                "type": "string",
                                "description": "Difficulty: 1-5. Default 3.",
                                "enum": ["1", "2", "3", "4", "5"]
                            },
                            "state": {
                                "type": "string",
                                "description": "Initial state. Default 'New'.",
                                "enum": ["New", "Active", "In Progress", "Resolved", "Closed"]
                            },
                            "level": {
                                "type": "string",
                                "description": "Work item type/level.",
                                "enum": ["Domain", "Module", "Feature", "Component", "Bug", "Task"]
                            },
                            "parent_id": {
                                "type": ["integer", "string"],
                                "description": "Parent work-item number (integer), or a batch index ref like '@2' to use the item created at index 2 in this call. Omit for root-level items."
                            },
                            "tags": {
                                "type": "array",
                                "items": { "type": "string" },
                                "description": "Optional tags."
                            }
                        },
                        "required": ["title"]
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

        // Pre-load levels once
        var levels = await workItemLevelService.GetByProjectIdAsync(context.ProjectId);
        var results = new List<object>();
        var createdNumbers = new List<int>(); // tracks WIT numbers by batch index for @N refs

        foreach (var item in itemsEl.EnumerateArray())
        {
            try
            {
                var title = UpdateWorkItemTool.GetString(item, "title") ?? "Untitled";
                int? levelId = null;
                var levelName = UpdateWorkItemTool.GetString(item, "level");
                if (!string.IsNullOrWhiteSpace(levelName))
                    levelId = levels.FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))?.Id;

                var parentId = UpdateWorkItemTool.ResolveParentId(item, createdNumbers);

                var request = new Models.CreateWorkItemRequest(
                    Title: title,
                    Description: UpdateWorkItemTool.GetString(item, "description") ?? "",
                    Priority: UpdateWorkItemTool.GetInt(item, "priority") ?? 3,
                    Difficulty: UpdateWorkItemTool.GetInt(item, "difficulty") ?? 3,
                    State: UpdateWorkItemTool.GetString(item, "state") ?? "New",
                    AssignedTo: "Unassigned",
                    Tags: UpdateWorkItemTool.GetStringArray(item, "tags") ?? [],
                    IsAI: false,
                    ParentWorkItemNumber: parentId,
                    LevelId: levelId
                );

                var created = await workItemService.CreateAsync(context.ProjectId, request);
                createdNumbers.Add(created.WorkItemNumber);
                results.Add(new
                {
                    Index = createdNumbers.Count - 1,
                    Id = created.WorkItemNumber,
                    created.Title,
                    created.State,
                    created.Priority,
                    created.Difficulty,
                    Level = levelName,
                    ParentId = created.ParentWorkItemNumber,
                });
            }
            catch (Exception ex)
            {
                createdNumbers.Add(0); // placeholder so subsequent @N indices stay correct
                results.Add(new { Error = ex.Message, Title = UpdateWorkItemTool.GetString(item, "title") });
            }
        }

        return JsonSerializer.Serialize(new { Created = results.Count, Results = results },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
