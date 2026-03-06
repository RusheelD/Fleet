namespace Fleet.Server.Data.Entities;

public class WorkItem
{
    public int Id { get; set; }

    /// <summary>Project-scoped sequential number (displayed in the UI).</summary>
    public int WorkItemNumber { get; set; }

    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Difficulty { get; set; } = 3;
    public string AssignedTo { get; set; } = string.Empty;
    public bool IsAI { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AssignmentMode { get; set; } = "auto";
    public int? AssignedAgentCount { get; set; }
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public string? LinkedPullRequestUrl { get; set; }

    // Stored as PostgreSQL text[] array — native array support
    public List<string> Tags { get; set; } = [];

    // Work item level (type/tier)
    public int? LevelId { get; set; }
    public WorkItemLevel? Level { get; set; }

    // Parent/child hierarchy (self-referencing)
    public int? ParentId { get; set; }
    public WorkItem? Parent { get; set; }
    public List<WorkItem> Children { get; set; } = [];

    // Foreign key
    public string ProjectId { get; set; } = string.Empty;
    public Project Project { get; set; } = null!;
}
