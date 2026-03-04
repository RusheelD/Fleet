namespace Fleet.Server.Data.Entities;

/// <summary>
/// Owned entity stored as JSON within UserProfile.
/// </summary>
public class UserPreferences
{
    public bool AgentCompletedNotification { get; set; }
    public bool PrOpenedNotification { get; set; }
    public bool AgentErrorsNotification { get; set; }
    public bool WorkItemUpdatesNotification { get; set; }
    public bool DarkMode { get; set; } = true;
    public bool CompactMode { get; set; }
    public bool SidebarCollapsed { get; set; }
}
