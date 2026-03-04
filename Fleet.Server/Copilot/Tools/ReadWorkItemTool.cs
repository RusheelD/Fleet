using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Reads a single work item by its project-scoped number.</summary>
public class ReadWorkItemTool(IWorkItemService workItemService) : IChatTool
{
    public string Name => "read_work_item";

    public string Description =>
        "Read a single work item by its project-scoped number. Returns full details including description, tags, parent, and children.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "id": {
                    "type": "integer",
                    "description": "Work-item number to read."
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

        var item = await workItemService.GetByWorkItemNumberAsync(context.ProjectId, id);
        if (item is null) return $"Error: work item #{id} not found.";

        return JsonSerializer.Serialize(new
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
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
