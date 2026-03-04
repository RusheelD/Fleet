namespace Fleet.Server.Models;

public record WorkItemDto(
    int WorkItemNumber,
    string Title,
    string State,
    int Priority,
    string AssignedTo,
    string[] Tags,
    bool IsAI,
    string Description,
    int? ParentWorkItemNumber,
    int[] ChildWorkItemNumbers,
    int? LevelId
);

public record CreateWorkItemRequest(
    string Title,
    string Description,
    int Priority,
    string State,
    string AssignedTo,
    string[] Tags,
    bool IsAI,
    int? ParentWorkItemNumber,
    int? LevelId
);

public record UpdateWorkItemRequest(
    string? Title,
    string? Description,
    int? Priority,
    string? State,
    string? AssignedTo,
    string[]? Tags,
    bool? IsAI,
    int? ParentWorkItemNumber,
    int? LevelId
);

public record WorkItemLevelDto(
    int Id,
    string Name,
    string IconName,
    string Color,
    int Ordinal,
    bool IsDefault
);

public record CreateWorkItemLevelRequest(
    string Name,
    string IconName,
    string Color,
    int Ordinal
);

public record UpdateWorkItemLevelRequest(
    string? Name,
    string? IconName,
    string? Color,
    int? Ordinal
);
