using System.Text.Json;
using Fleet.Server.WorkItems;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Creates a new work item in the current project.</summary>
public class CreateWorkItemTool(IWorkItemService workItemService, IWorkItemLevelService workItemLevelService) : IChatTool
{
    public string Name => "create_work_item";

    public bool IsWriteTool => true;

    public string Description =>
        "Create a new work item in the current project. Returns the created work item. " +
        "Valid states: New, Active, Planning (AI), In Progress, In Progress (AI), In-PR, In-PR (AI), Resolved, Resolved (AI), Closed. Priority: 1 (critical) to 4 (low). " +
        "Valid levels (types): Domain, Module, Feature, Component, Bug, Task. " +
        "Use parent_id to nest under an existing work item (e.g. Tasks under a Feature).";

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
                    "type": "string",
                    "description": "Priority: 1 (critical), 2 (high), 3 (medium), 4 (low). Default 3.",
                    "enum": ["1", "2", "3", "4"]
                },
                "difficulty": {
                    "type": "string",
                    "description": "Difficulty: 1 (very easy), 2 (easy), 3 (medium), 4 (hard), 5 (very hard). Default 3.",
                    "enum": ["1", "2", "3", "4", "5"]
                },
                "state": {
                    "type": "string",
                    "description": "Initial state. Default 'New'.",
                    "enum": ["New", "Active", "Planning (AI)", "In Progress", "In Progress (AI)", "In-PR", "In-PR (AI)", "Resolved", "Resolved (AI)", "Closed"]
                },
                "level": {
                    "type": "string",
                    "description": "Work item type/level. Use to categorize: Domain (top-level), Module, Feature, Component, Bug, Task.",
                    "enum": ["Domain", "Module", "Feature", "Component", "Bug", "Task"]
                },
                "parent_id": {
                    "type": "integer",
                    "description": "Work-item number of the parent to nest this under (the project-scoped number shown in the UI). Omit for root-level items."
                },
                "root_justification": {
                    "type": "string",
                    "description": "Dynamic Iteration only: required when creating a root-level item in a project that already has work items. Explain why no existing parent is appropriate."
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
        if (!context.TryGetProjectId(out var projectId))
            return ChatToolContext.ProjectScopeRequiredMessage;

        var args = ParseArgs(argumentsJson);
        var placementError = await DynamicIterationWorkItemPlacementGuard.ValidateCreatePlacementAsync(
            workItemService,
            projectId,
            context,
            args.ParentId,
            args.RootJustification);
        if (placementError is not null)
            return placementError;

        // Resolve level name to ID
        int? levelId = null;
        if (!string.IsNullOrWhiteSpace(args.Level))
        {
            var levels = await workItemLevelService.GetByProjectIdAsync(projectId);
            var match = levels.FirstOrDefault(l =>
                l.Name.Equals(args.Level, StringComparison.OrdinalIgnoreCase));
            levelId = match?.Id;
        }

        var request = new Models.CreateWorkItemRequest(
            Title: args.Title,
            Description: UpdateWorkItemTool.MergeAttachmentReferencesIntoDescription(
                args.Description,
                context.CurrentMessageAttachments),
            Priority: args.Priority,
            Difficulty: args.Difficulty,
            State: args.State,
            AssignedTo: context.DefaultCreatedWorkItemAssignee,
            Tags: args.Tags,
            IsAI: context.DefaultCreatedWorkItemIsAi,
            ParentWorkItemNumber: args.ParentId,
            LevelId: levelId,
            AssignmentMode: context.DefaultCreatedWorkItemAssignmentMode
        );

        var created = await workItemService.CreateAsync(projectId, request);

        return JsonSerializer.Serialize(new
        {
            Id = created.WorkItemNumber,
            created.Title,
            created.State,
            created.Priority,
            created.Difficulty,
            created.Description,
            created.Tags,
            Level = args.Level,
            ParentId = args.ParentId,
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
            var priority = 3;
            if (root.TryGetProperty("priority", out var p))
            {
                // Some providers return string enums; others may return integers
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pv))
                    priority = pv;
                else if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var ps))
                    priority = ps;
            }
            var difficulty = 3;
            if (root.TryGetProperty("difficulty", out var df))
            {
                if (df.ValueKind == JsonValueKind.Number && df.TryGetInt32(out var dv))
                    difficulty = dv;
                else if (df.ValueKind == JsonValueKind.String && int.TryParse(df.GetString(), out var ds))
                    difficulty = ds;
            }
            var state = root.TryGetProperty("state", out var s) ? s.GetString() ?? "New" : "New";
            var level = root.TryGetProperty("level", out var lv) ? lv.GetString() : null;

            int? parentId = null;
            if (root.TryGetProperty("parent_id", out var pid))
            {
                if (pid.ValueKind == JsonValueKind.Number && pid.TryGetInt32(out var pidVal))
                    parentId = pidVal;
                else if (pid.ValueKind == JsonValueKind.String && int.TryParse(pid.GetString(), out var pidStr))
                    parentId = pidStr;
            }

            string[] tags = [];
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                tags = tagsEl.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(s2 => !string.IsNullOrWhiteSpace(s2))
                    .ToArray();
            }

            var rootJustification = UpdateWorkItemTool.GetString(
                root,
                DynamicIterationWorkItemPlacementGuard.RootJustificationPropertyName);

            return new CreateWorkItemArgs(title, description, priority, difficulty, state, level, parentId, tags, rootJustification);
        }
        catch
        {
            return new CreateWorkItemArgs("Untitled", null, 3, 3, "New", null, null, [], null);
        }
    }

    private record CreateWorkItemArgs(
        string Title,
        string? Description,
        int Priority,
        int Difficulty,
        string State,
        string? Level,
        int? ParentId,
        string[] Tags,
        string? RootJustification);
}
