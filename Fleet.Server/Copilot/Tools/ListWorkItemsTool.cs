using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists work items for the current project, with optional state filter.</summary>
public class ListWorkItemsTool(IWorkItemService workItemService) : IChatTool
{
    public string Name => "list_work_items";

    public string Description =>
        "List work items in the current project. Optionally filter by state (e.g., 'New', 'Active', 'In Progress', 'Resolved', 'Closed'). Returns id, title, state, priority, assignedTo, and tags.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "state": {
                    "type": "string",
                    "description": "Optional filter: only return work items with this state."
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
        var items = await workItemService.GetByProjectIdAsync(context.ProjectId);

        IEnumerable<object> result = items.Select(i => new
        {
            i.Id,
            i.Title,
            i.State,
            i.Priority,
            i.AssignedTo,
            i.Tags,
            i.IsAI,
        });

        if (!string.IsNullOrWhiteSpace(args.State))
            result = result.Where(i => ((string)((dynamic)i).State).Equals(args.State, StringComparison.OrdinalIgnoreCase));

        var materialised = result.Take(args.Limit).ToList();

        return materialised.Count == 0
            ? "No work items found matching the criteria."
            : JsonSerializer.Serialize(materialised, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ListWorkItemsArgs ParseArgs(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var state = root.TryGetProperty("state", out var s) ? s.GetString() : null;
            var limit = root.TryGetProperty("limit", out var l) && l.TryGetInt32(out var lv) ? lv : 20;
            return new ListWorkItemsArgs(state, limit);
        }
        catch
        {
            return new ListWorkItemsArgs(null, 20);
        }
    }

    private record ListWorkItemsArgs(string? State, int Limit);
}
