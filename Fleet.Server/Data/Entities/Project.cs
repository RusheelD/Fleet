namespace Fleet.Server.Data.Entities;

public class Project
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = "default";
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string LastActivity { get; set; } = string.Empty;
    public string BranchPattern { get; set; } = "fleet/{workItemNumber}-{slug}";
    public string CommitAuthorMode { get; set; } = "fleet";
    public string? CommitAuthorName { get; set; }
    public string? CommitAuthorEmail { get; set; }

    // Stored as JSON (jsonb) columns — demonstrates PostgreSQL JSON support
    public WorkItemSummary WorkItemSummary { get; set; } = new();
    public AgentSummary AgentSummary { get; set; } = new();

    // Navigation properties
    public List<WorkItem> WorkItems { get; set; } = [];
    public List<WorkItemLevel> WorkItemLevels { get; set; } = [];
    public List<ChatSession> ChatSessions { get; set; } = [];
    public List<AgentExecution> AgentExecutions { get; set; } = [];
    public List<LogEntry> LogEntries { get; set; } = [];
    public List<DashboardAgent> DashboardAgents { get; set; } = [];
    public List<MemoryEntry> MemoryEntries { get; set; } = [];
}
