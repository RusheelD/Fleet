using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Deletes a work item by its project-scoped number.</summary>
public class DeleteWorkItemTool(IWorkItemService workItemService) : IChatTool
{
    public string Name => "delete_work_item";

    public bool IsWriteTool => true;

    public string Description =>
        "Delete a work item by its project-scoped number.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "id": {
                    "type": "integer",
                    "description": "Work-item number to delete."
                }
            },
            "required": ["id"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = JsonDocument.Parse(argumentsJson).RootElement;
        var id = UpdateWorkItemTool.GetInt(args, "id") ?? 0;
        if (id <= 0) return "Error: 'id' (work-item number) is required.";

        var deleted = await workItemService.DeleteAsync(context.ProjectId, id);
        return deleted
            ? JsonSerializer.Serialize(new { Id = id, Deleted = true })
            : $"Error: work item #{id} not found.";
    }
}
