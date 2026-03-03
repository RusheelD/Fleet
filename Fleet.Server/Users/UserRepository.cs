using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Users;

public class UserRepository(FleetDbContext context, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<UserProfileDto?> GetProfileAsync(int userId)
    {
        var user = await context.UserProfiles.FindAsync(userId);
        if (user is null) return null;
        return new UserProfileDto(user.DisplayName, user.Email, user.Bio, user.Location, user.AvatarUrl);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await context.UserProfiles.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.Bio = request.Bio;
        user.Location = request.Location;
        await context.SaveChangesAsync();
        return new UserProfileDto(user.DisplayName, user.Email, user.Bio, user.Location, user.AvatarUrl);
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId)
    {
        return await context.LinkedAccounts
            .Where(a => a.UserProfileId == userId)
            .Select(a => new LinkedAccountDto(a.Provider, a.ConnectedAs, a.ExternalUserId, a.ConnectedAt))
            .ToListAsync();
    }

    public async Task<UserPreferencesDto?> GetPreferencesAsync(int userId)
    {
        var user = await context.UserProfiles.FindAsync(userId);
        if (user is null) return null;
        var p = user.Preferences;
        return new UserPreferencesDto(
            p.AgentCompletedNotification, p.PrOpenedNotification, p.AgentErrorsNotification,
            p.WorkItemUpdatesNotification, p.DarkMode, p.CompactMode, p.SidebarCollapsed
        );
    }

    public async Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UserPreferencesDto preferences)
    {
        var user = await context.UserProfiles.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        user.Preferences = new UserPreferences
        {
            AgentCompletedNotification = preferences.AgentCompletedNotification,
            PrOpenedNotification = preferences.PrOpenedNotification,
            AgentErrorsNotification = preferences.AgentErrorsNotification,
            WorkItemUpdatesNotification = preferences.WorkItemUpdatesNotification,
            DarkMode = preferences.DarkMode,
            CompactMode = preferences.CompactMode,
            SidebarCollapsed = preferences.SidebarCollapsed,
        };
        await context.SaveChangesAsync();
        logger.LogInformation("Updated preferences for user {UserId}", userId);
        return preferences;
    }
}
