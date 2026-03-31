using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Deletes multiple work items in a single tool call.</summary>
public class BulkDeleteWorkItemsTool(IWorkItemService workItemService) : IChatTool
{
    public string Name => "bulk_delete_work_items";

    public bool IsWriteTool => true;

    public string Description =>
        "Delete multiple work items in a single call. Provide an array of work-item numbers. " +
        "Returns results for each deletion (success or not-found). Use this instead of calling delete_work_item in a loop.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "ids": {
                    "type": "array",
                    "description": "Array of work-item numbers to delete.",
                    "items": { "type": "integer" }
                }
            },
            "required": ["ids"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!context.TryGetProjectId(out var projectId))
            return ChatToolContext.ProjectScopeRequiredMessage;

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
                var deleted = await workItemService.DeleteAsync(projectId, id);
                results.Add(new { Id = id, Deleted = deleted });
            }
            catch (Exception ex)
            {
                results.Add(new { Id = id, Error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new { Processed = results.Count, Results = results },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
