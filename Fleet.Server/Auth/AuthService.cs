using System.Security.Claims;
using Fleet.Server.Data.Entities;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Auth;

public class AuthService(
    IAuthRepository authRepository,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private int? _resolvedUserId;

    public async Task<UserProfileDto> GetOrCreateCurrentUserAsync()
    {
        var user = await ResolveCurrentUserAsync();
        return ToProfileDto(user);
    }

    public async Task<int> GetCurrentUserIdAsync()
    {
        if (_resolvedUserId.HasValue)
            return _resolvedUserId.Value;

        var user = await ResolveCurrentUserAsync();
        _resolvedUserId = user.Id;
        return user.Id;
    }

    private async Task<UserProfile> ResolveCurrentUserAsync()
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No authenticated user.");

        var oid = principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? principal.FindFirst("oid")?.Value;

        if (string.IsNullOrEmpty(oid))
            throw new UnauthorizedAccessException("No object identifier claim found in token.");

        var rawEmail = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("preferred_username")?.Value
            ?? "";
        var email = NormalizeEmail(rawEmail, oid);
        var isUnlimitedTierUser = IsUnlimitedTierUser(oid, rawEmail);

        var existing = await authRepository.GetByEntraObjectIdAsync(oid);
        if (existing is not null)
        {
            var needsUpdate = false;
            var normalizedRole = UserRoles.Normalize(existing.Role);
            if (!string.Equals(existing.Role, normalizedRole, StringComparison.Ordinal))
            {
                existing.Role = normalizedRole;
                needsUpdate = true;
            }

            if (isUnlimitedTierUser && !UserRoles.IsUnlimited(normalizedRole))
            {
                existing.Role = UserRoles.Unlimited;
                needsUpdate = true;
            }

            if (needsUpdate)
                await authRepository.UpdateUserAsync(existing);

            logger.AuthResolvedUser(oid.SanitizeForLogging(), existing.Id);
            _resolvedUserId = existing.Id;
            return existing;
        }

        // Auto-provision a new local profile from Entra ID claims
        var name = principal.FindFirst("name")?.Value
            ?? principal.Identity?.Name
            ?? "User";

        var user = new UserProfile
        {
            EntraObjectId = oid,
            Username = DeriveUsername(rawEmail, name),
            Email = email,
            DisplayName = name,
            Bio = string.Empty,
            Location = string.Empty,
            AvatarUrl = string.Empty,
            Role = isUnlimitedTierUser ? UserRoles.Unlimited : UserRoles.Free,
            CreatedAt = DateTime.UtcNow,
            Preferences = new UserPreferences()
        };

        try
        {
            await authRepository.CreateUserAsync(user);
            logger.AuthAutoProvisionedUser(user.Id, oid.SanitizeForLogging(), email.SanitizeForLogging());
            _resolvedUserId = user.Id;
            return user;
        }
        catch (DbUpdateException)
        {
            // A concurrent request already created this user (race condition on first login).
            // Re-fetch by OID first, then fall back to email match.
            var concurrentlyCreated = await authRepository.GetByEntraObjectIdAsync(oid)
                ?? await authRepository.GetByEmailAsync(email);
            if (concurrentlyCreated is not null)
            {
                // If found by email but OID differs, update OID so future lookups are fast
                if (concurrentlyCreated.EntraObjectId != oid)
                {
                    concurrentlyCreated.EntraObjectId = oid;
                }

                if (string.IsNullOrWhiteSpace(concurrentlyCreated.Role))
                    concurrentlyCreated.Role = UserRoles.Free;

                concurrentlyCreated.Role = UserRoles.Normalize(concurrentlyCreated.Role);

                if (isUnlimitedTierUser && !UserRoles.IsUnlimited(concurrentlyCreated.Role))
                {
                    concurrentlyCreated.Role = UserRoles.Unlimited;
                }

                await authRepository.UpdateUserAsync(concurrentlyCreated);

                logger.AuthResolvedUser(oid.SanitizeForLogging(), concurrentlyCreated.Id);
                _resolvedUserId = concurrentlyCreated.Id;
                return concurrentlyCreated;
            }

            // If still not found, the constraint violation was for a different reason — rethrow.
            throw;
        }
    }

    private static string DeriveUsername(string email, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            return email.Split('@')[0].ToLowerInvariant();

        return displayName.ToLowerInvariant().Replace(' ', '.');
    }

    private static string NormalizeEmail(string email, string oid)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        // Some identity tokens omit email. Use a stable per-user placeholder to
        // satisfy the unique email constraint without conflating distinct users.
        var safeOid = (oid ?? string.Empty).Trim().ToLowerInvariant();
        return $"{safeOid}@entra.local";
    }

    private static UserProfileDto ToProfileDto(UserProfile user) =>
        new(
            user.DisplayName,
            user.Email,
            user.Bio,
            user.Location,
            user.AvatarUrl,
            UserRoles.Normalize(user.Role));

    private bool IsUnlimitedTierUser(string oid, string email)
    {
        if (IsAdminIdentity(oid, email))
            return true;

        var unlimitedObjectIds = configuration.GetSection("UserRoles:UnlimitedEntraObjectIds").Get<string[]>() ?? [];
        if (unlimitedObjectIds.Contains(oid, StringComparer.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        // Requested hard override: this identity always receives unlimited tier.
        if (string.Equals(email, "rusheel@live.com", StringComparison.OrdinalIgnoreCase))
            return true;

        var unlimitedEmails = configuration.GetSection("UserRoles:UnlimitedEmails").Get<string[]>() ?? [];
        return unlimitedEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAdminIdentity(string oid, string email)
    {
        var adminObjectIds = configuration.GetSection("Admin:AllowedEntraObjectIds").Get<string[]>() ?? [];
        if (!string.IsNullOrWhiteSpace(oid) && adminObjectIds.Contains(oid, StringComparer.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        if (string.Equals(email, "rusheel@live.com", StringComparison.OrdinalIgnoreCase))
            return true;

        var adminEmails = configuration.GetSection("Admin:AllowedEmails").Get<string[]>() ?? [];
        return adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }
}
