namespace Fleet.Server.Models;

public record CreateWorkItemRequest(
    string Title,
    string Description,
    int Priority,
    int Difficulty,
    string State,
    string AssignedTo,
    string[] Tags,
    bool IsAI,
    int? ParentWorkItemNumber,
    int? LevelId,
    string? AssignmentMode = null,
    int? AssignedAgentCount = null,
    string? AcceptanceCriteria = null
);
