using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Creates a new work item in the current project.</summary>
public class CreateWorkItemTool(IWorkItemService workItemService) : IChatTool
{
    public string Name => "create_work_item";

    public string Description =>
        "Create a new work item in the current project. Returns the created work item. " +
        "Valid states: New, Active, In Progress, Resolved, Closed. Priority: 1 (critical) to 4 (low).";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "title": {
                    "type": "string",
                    "description": "Title of the work item."
                },
                "description": {
                    "type": "string",
                    "description": "Detailed description of the work item."
                },
                "priority": {
                    "type": "integer",
                    "description": "Priority: 1 (critical), 2 (high), 3 (medium), 4 (low). Default 3.",
                    "enum": [1, 2, 3, 4]
                },
                "state": {
                    "type": "string",
                    "description": "Initial state. Default 'New'.",
                    "enum": ["New", "Active", "In Progress", "Resolved", "Closed"]
                },
                "tags": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Optional tags."
                }
            },
            "required": ["title"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);

        var request = new Models.CreateWorkItemRequest(
            Title: args.Title,
            Description: args.Description ?? "",
            Priority: args.Priority,
            State: args.State,
            AssignedTo: "Unassigned",
            Tags: args.Tags,
            IsAI: false,
            ParentId: null,
            LevelId: null
        );

        var created = await workItemService.CreateAsync(context.ProjectId, request);

        return JsonSerializer.Serialize(new
        {
            created.Id,
            created.Title,
            created.State,
            created.Priority,
            created.Description,
            created.Tags,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static CreateWorkItemArgs ParseArgs(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Untitled" : "Untitled";
            var description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            var priority = root.TryGetProperty("priority", out var p) && p.TryGetInt32(out var pv) ? pv : 3;
            var state = root.TryGetProperty("state", out var s) ? s.GetString() ?? "New" : "New";

            string[] tags = [];
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                tags = tagsEl.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(s2 => !string.IsNullOrWhiteSpace(s2))
                    .ToArray();
            }

            return new CreateWorkItemArgs(title, description, priority, state, tags);
        }
        catch
        {
            return new CreateWorkItemArgs("Untitled", null, 3, "New", []);
        }
    }

    private record CreateWorkItemArgs(string Title, string? Description, int Priority, string State, string[] Tags);
}
