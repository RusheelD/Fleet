using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Reads multiple work items in a single tool call.</summary>
public class BulkReadWorkItemsTool(IWorkItemService workItemService) : IChatTool
{
    public string Name => "bulk_read_work_items";

    public string Description =>
        "Read multiple work items by their project-scoped numbers in a single call. " +
        "Returns full details for each item, or an error for items not found.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "ids": {
                    "type": "array",
                    "description": "Array of work-item numbers to read.",
                    "items": { "type": "integer" }
                }
            },
            "required": ["ids"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var root = JsonDocument.Parse(argumentsJson).RootElement;
        if (!root.TryGetProperty("ids", out var idsEl) || idsEl.ValueKind != JsonValueKind.Array)
            return "Error: 'ids' array is required.";

        var results = new List<object>();
        foreach (var idEl in idsEl.EnumerateArray())
        {
            int id = 0;
            if (idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var n)) id = n;
            else if (idEl.ValueKind == JsonValueKind.String && int.TryParse(idEl.GetString(), out var s)) id = s;

            if (id <= 0) { results.Add(new { Id = id, Error = "Invalid id." }); continue; }

            try
            {
                var item = await workItemService.GetByWorkItemNumberAsync(context.ProjectId, id);
                if (item is null)
                {
                    results.Add(new { Id = id, Error = "Not found." });
                }
                else
                {
                    results.Add(new
                    {
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
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new { Id = id, Error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new { Count = results.Count, Results = results },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
