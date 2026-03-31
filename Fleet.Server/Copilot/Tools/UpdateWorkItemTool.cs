using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Updates an existing work item by its project-scoped number.</summary>
public class UpdateWorkItemTool(IWorkItemService workItemService, IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "update_work_item";

    public bool IsWriteTool => true;

    public string Description =>
        "Update an existing work item by its project-scoped number. Only supply fields you want to change. " +
        "Valid states: New, Active, Planning (AI), In Progress, In Progress (AI), In-PR, In-PR (AI), Resolved, Resolved (AI), Closed. Priority: 1 (critical) to 4 (low). " +
        "Valid levels (types): Domain, Module, Feature, Component, Bug, Task. " +
        "Set parent_id to 0 to clear the parent.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "id": {
                    "type": "integer",
                    "description": "Work-item number to update (the project-scoped number shown in the UI)."
                },
                "title": { "type": "string", "description": "New title." },
                "description": { "type": "string", "description": "New description." },
                "priority": {
                    "type": "string",
                    "description": "New priority: 1-4.",
                    "enum": ["1", "2", "3", "4"]
                },
                "difficulty": {
                    "type": "string",
                    "description": "New difficulty: 1-5.",
                    "enum": ["1", "2", "3", "4", "5"]
                },
                "state": {
                    "type": "string",
                    "description": "New state.",
                    "enum": ["New", "Active", "Planning (AI)", "In Progress", "In Progress (AI)", "In-PR", "In-PR (AI)", "Resolved", "Resolved (AI)", "Closed"]
                },
                "level": {
                    "type": "string",
                    "description": "New work item type/level.",
                    "enum": ["Domain", "Module", "Feature", "Component", "Bug", "Task"]
                },
                "parent_id": {
                    "type": "integer",
                    "description": "New parent work-item number. Set to 0 to clear parent."
                },
                "tags": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Replace tags with this list."
                }
            },
            "required": ["id"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!context.TryGetProjectId(out var projectId))
            return ChatToolContext.ProjectScopeRequiredMessage;

        var args = JsonDocument.Parse(argumentsJson).RootElement;
        var id = GetInt(args, "id") ?? 0;
        if (id <= 0) return "Error: 'id' (work-item number) is required.";

        int? levelId = null;
        if (args.TryGetProperty("level", out var lvProp) && lvProp.GetString() is string lvName)
        {
            var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
            levelId = levels.FirstOrDefault(l => l.Name.Equals(lvName, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        var request = new Models.UpdateWorkItemRequest(
            Title: GetString(args, "title"),
            Description: GetString(args, "description"),
            Priority: GetInt(args, "priority"),
            Difficulty: GetInt(args, "difficulty"),
            State: GetString(args, "state"),
            AssignedTo: null,
            Tags: GetStringArray(args, "tags"),
            IsAI: null,
            ParentWorkItemNumber: GetInt(args, "parent_id"),
            LevelId: levelId
        );

        var updated = await workItemService.UpdateAsync(projectId, id, request);
        if (updated is null) return $"Error: work item #{id} not found.";

        return JsonSerializer.Serialize(new
        {
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

    internal static string? GetString(JsonElement root, string prop)
    {
        return root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    internal static int? GetInt(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return null;
    }

    internal static string[]? GetStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array) return null;
        return v.EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    /// <summary>
    /// Resolves a parent_id value that may be an integer (existing WIT number)
    /// or a string like "@2" referencing the item at index 2 in the current batch.
    /// </summary>
    internal static int? ResolveParentId(JsonElement item, IReadOnlyList<int> createdNumbers)
    {
        if (!item.TryGetProperty("parent_id", out var v)) return null;

        // String form: "@N" → batch index reference
        if (v.ValueKind == JsonValueKind.String)
        {
            var raw = v.GetString();
            if (raw is not null && raw.StartsWith('@') && int.TryParse(raw.AsSpan(1), out var idx))
            {
                if (idx >= 0 && idx < createdNumbers.Count)
                    return createdNumbers[idx];
                return null; // invalid forward reference — ignore
            }
            // Plain numeric string
            if (int.TryParse(raw, out var num)) return num;
            return null;
        }

        // Integer form: direct WIT number
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        return null;
    }
}
