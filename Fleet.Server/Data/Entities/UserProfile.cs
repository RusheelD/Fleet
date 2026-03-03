namespace Fleet.Server.Data.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string EntraObjectId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Stored as JSON (jsonb) — demonstrates embedded document storage
    public UserPreferences Preferences { get; set; } = new();

    // Navigation
    public List<LinkedAccount> LinkedAccounts { get; set; } = [];
}

/// <summary>
/// Owned entity stored as JSON within UserProfile.
/// </summary>
public class UserPreferences
{
    public bool AgentCompletedNotification { get; set; }
    public bool PrOpenedNotification { get; set; }
    public bool AgentErrorsNotification { get; set; }
    public bool WorkItemUpdatesNotification { get; set; }
    public bool DarkMode { get; set; }
    public bool CompactMode { get; set; }
    public bool SidebarCollapsed { get; set; }
}
