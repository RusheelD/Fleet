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
}

public class WorkItemSummary
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Resolved { get; set; }
}

public class AgentSummary
{
    public int Total { get; set; }
    public int Running { get; set; }
}
