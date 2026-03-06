namespace Fleet.Server.Models;

public record WorkItemDto(
    int WorkItemNumber,
    string Title,
    string State,
    int Priority,
    int Difficulty,
    string AssignedTo,
    string[] Tags,
    bool IsAI,
    string Description,
    int? ParentWorkItemNumber,
    int[] ChildWorkItemNumbers,
    int? LevelId,
    string AssignmentMode = "auto",
    int? AssignedAgentCount = null,
    string AcceptanceCriteria = "",
    string? LinkedPullRequestUrl = null
);
