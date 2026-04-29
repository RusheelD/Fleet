using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Fleet.Server.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fleet.Server.Users;

public class UserRepository(FleetDbContext context, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<UserProfileDto?> GetProfileAsync(int userId)
    {
        var user = await context.UserProfiles.FindAsync(userId);
        if (user is null) return null;
        return ToProfileDto(user);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await context.UserProfiles.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!LoginIdentityClaims.IsDisplayableEmail(normalizedEmail, user.EntraObjectId))
            throw new ArgumentException("Enter a valid email address.");

        var emailInUse = await context.UserProfiles
            .AnyAsync(u => u.Id != userId && u.Email == normalizedEmail);
        if (emailInUse)
            throw new InvalidOperationException("That email address is already in use by another account.");

        user.DisplayName = request.DisplayName;
        user.Email = normalizedEmail;
        user.Bio = request.Bio;
        user.Location = request.Location;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                           pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                                           string.Equals(pg.ConstraintName, "IX_UserProfiles_Email", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("That email address is already in use by another account.", ex);
        }

        return ToProfileDto(user);
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId)
    {
        return await context.LinkedAccounts
            .Where(a => a.UserProfileId == userId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.ConnectedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new LinkedAccountDto(a.Id, a.Provider, a.ConnectedAs, a.ExternalUserId, a.ConnectedAt, a.IsPrimary))
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
        logger.UsersPreferencesUpdated(userId);
        return preferences;
    }

    private static UserProfileDto ToProfileDto(UserProfile user) =>
        new(
            user.DisplayName,
            LoginIdentityClaims.NormalizeDisplayEmail(user.Email, user.EntraObjectId) ?? string.Empty,
            user.Bio,
            user.Location,
            user.AvatarUrl,
            UserRoles.Normalize(user.Role));
}
