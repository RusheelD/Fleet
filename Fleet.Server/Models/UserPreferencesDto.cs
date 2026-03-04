namespace Fleet.Server.Models;

public record UserPreferencesDto(
    bool AgentCompletedNotification,
    bool PrOpenedNotification,
    bool AgentErrorsNotification,
    bool WorkItemUpdatesNotification,
    bool DarkMode,
    bool CompactMode,
    bool SidebarCollapsed
);
