namespace Fleet.Server.Realtime;

public static class ServerEventTopics
{
    public const string Connected = "connected";
    public const string NotificationsUpdated = "notifications.updated";
    public const string ChatUpdated = "chat.updated";
    public const string ChatToolEvent = "chat.tool-event";
    public const string AgentsUpdated = "agents.updated";
    public const string LogsUpdated = "logs.updated";
    public const string WorkItemsUpdated = "work-items.updated";
    public const string ProjectsUpdated = "projects.updated";
}
