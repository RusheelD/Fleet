namespace Fleet.Server.Models;

public record UpdateWorkItemRequest(
    string? Title,
    string? Description,
    int? Priority,
    int? Difficulty,
    string? State,
    string? AssignedTo,
    string[]? Tags,
    bool? IsAI,
    int? ParentWorkItemNumber,
    int? LevelId
);
