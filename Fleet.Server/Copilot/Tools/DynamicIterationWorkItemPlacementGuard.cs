using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

internal static class DynamicIterationWorkItemPlacementGuard
{
    public const string RootJustificationPropertyName = "root_justification";

    public static async Task<string?> ValidateCreatePlacementAsync(
        IWorkItemService workItemService,
        string projectId,
        ChatToolContext context,
        int? parentWorkItemNumber,
        string? rootJustification)
    {
        if (!context.DynamicIterationEnabled)
            return null;

        if (parentWorkItemNumber is > 0)
            return null;

        var existingWorkItems = await workItemService.GetByProjectIdAsync(projectId);
        if (existingWorkItems.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(rootJustification))
            return null;

        return "Error: Dynamic Iteration root-level creation requires a parent_id when an appropriate existing parent exists. If no existing work item is an appropriate parent, include root_justification explaining why this item must be a new root.";
    }
}
