namespace Fleet.Server.Models;

public record UserProfileDto(
    string DisplayName,
    string Email,
    string Bio,
    string Location,
    string AvatarUrl
);

public record UpdateProfileRequest(
    string DisplayName,
    string Email,
    string Bio,
    string Location
);

public record LinkedAccountDto(
    string Provider,
    string? ConnectedAs,
    string? ExternalUserId = null,
    DateTime? ConnectedAt = null
);

public record LinkGitHubRequest(string Code, string RedirectUri);

public record GitHubRepoDto(string FullName, string Name, string Owner, string? Description, bool Private, string HtmlUrl);

public record UserPreferencesDto(
    bool AgentCompletedNotification,
    bool PrOpenedNotification,
    bool AgentErrorsNotification,
    bool WorkItemUpdatesNotification,
    bool DarkMode,
    bool CompactMode,
    bool SidebarCollapsed
);

public record UserSettingsDto(
    UserProfileDto Profile,
    LinkedAccountDto[] Connections,
    UserPreferencesDto Preferences
);

// ── Auth DTOs ──────────────────────────────────────────────

// AuthResponse is no longer needed — the /api/auth/me endpoint returns UserProfileDto directly.
