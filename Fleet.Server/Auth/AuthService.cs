using System.Security.Cryptography;
using System.Text.Json;
using Fleet.Server.Data.Entities;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Auth;

public class AuthService(
    IAuthRepository authRepository,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<AuthService> logger,
    IDataProtectionProvider? dataProtectionProvider = null) : IAuthService
{
    private static readonly TimeSpan LoginProviderLinkStateLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LoginIdentityUsageUpdateInterval = TimeSpan.FromMinutes(15);
    private readonly IDataProtector _loginProviderLinkStateProtector =
        (dataProtectionProvider ?? DataProtectionProvider.Create("Fleet.AuthService"))
            .CreateProtector("Fleet.LoginProvider.LinkState.v1");
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

    public Task<LoginProviderLinkStateDto> CreateLoginProviderLinkStateAsync(int userId, string provider)
    {
        var normalizedProvider = NormalizeSupportedProvider(provider);
        var payload = new LoginProviderLinkStatePayload(
            userId,
            normalizedProvider,
            DateTimeOffset.UtcNow.Add(LoginProviderLinkStateLifetime).ToUnixTimeSeconds(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

        var state = _loginProviderLinkStateProtector.Protect(JsonSerializer.Serialize(payload));
        return Task.FromResult(new LoginProviderLinkStateDto(state));
    }

    public async Task<UserProfileDto> CompleteLoginProviderLinkAsync(string state)
    {
        var payload = ValidateLoginProviderLinkState(state);
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No authenticated user.");
        var currentIdentity = LoginIdentityClaims.Resolve(principal);

        if (!string.Equals(payload.Provider, currentIdentity.Provider, StringComparison.Ordinal))
            throw new InvalidOperationException($"The completed sign-in was for {currentIdentity.Provider}, but the link request was for {payload.Provider}.");

        var targetUser = await authRepository.GetByIdAsync(payload.UserId)
            ?? throw new InvalidOperationException("The account being linked no longer exists.");
        var existingIdentity = await authRepository.GetLoginIdentityAsync(currentIdentity.Provider, currentIdentity.ProviderUserId);
        if (existingIdentity is not null)
        {
            if (existingIdentity.UserProfileId != targetUser.Id)
                throw new InvalidOperationException("That sign-in method is already linked to another Fleet account.");

            await UpdateLoginIdentityUsageAsync(existingIdentity, currentIdentity);
            return ToProfileDto(targetUser);
        }

        var linkedIdentities = await authRepository.GetLoginIdentitiesAsync(targetUser.Id);
        if (linkedIdentities.Any(identity =>
                string.Equals(identity.Provider, currentIdentity.Provider, StringComparison.Ordinal) &&
                !string.Equals(identity.ProviderUserId, currentIdentity.ProviderUserId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A {currentIdentity.Provider} sign-in method is already linked. Delink it before linking a different one.");
        }

        await authRepository.CreateLoginIdentityAsync(CreateLoginIdentity(targetUser.Id, currentIdentity));
        return ToProfileDto(targetUser);
    }

    public async Task<IReadOnlyList<LoginIdentityDto>> GetLoginIdentitiesAsync(int userId)
    {
        var currentIdentity = TryResolveCurrentLoginIdentity();
        var identities = await authRepository.GetLoginIdentitiesAsync(userId);
        return identities
            .Select(identity => ToLoginIdentityDto(identity, currentIdentity))
            .ToArray();
    }

    public async Task DeleteLoginIdentityAsync(int userId, int identityId)
    {
        var identity = await authRepository.GetLoginIdentityByIdAsync(userId, identityId)
            ?? throw new InvalidOperationException("Sign-in method not found.");
        var identityCount = await authRepository.CountLoginIdentitiesAsync(userId);
        if (identityCount <= 1)
            throw new InvalidOperationException("At least one sign-in method must remain linked.");

        await authRepository.DeleteLoginIdentityAsync(identity);
    }

    private async Task<UserProfile> ResolveCurrentUserAsync()
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No authenticated user.");

        var currentIdentity = LoginIdentityClaims.Resolve(principal);
        var isUnlimitedTierUser = IsUnlimitedTierUser(currentIdentity.ProviderUserId, currentIdentity.Email);

        var linkedIdentity = await authRepository.GetLoginIdentityAsync(currentIdentity.Provider, currentIdentity.ProviderUserId);
        if (linkedIdentity?.UserProfile is not null)
        {
            await UpdateLoginIdentityUsageAsync(linkedIdentity, currentIdentity);
            await ApplyRoleUpdatesAsync(linkedIdentity.UserProfile, isUnlimitedTierUser);
            logger.AuthResolvedUser(currentIdentity.ProviderUserId.SanitizeForLogging(), linkedIdentity.UserProfileId);
            _resolvedUserId = linkedIdentity.UserProfileId;
            return linkedIdentity.UserProfile;
        }

        var existing = await authRepository.GetByEntraObjectIdAsync(currentIdentity.ProviderUserId);
        if (existing is not null)
        {
            await EnsureLoginIdentityAsync(existing.Id, currentIdentity);
            await ApplyRoleUpdatesAsync(existing, isUnlimitedTierUser);
            logger.AuthResolvedUser(currentIdentity.ProviderUserId.SanitizeForLogging(), existing.Id);
            _resolvedUserId = existing.Id;
            return existing;
        }

        var existingEmailUser = await authRepository.GetByEmailAsync(currentIdentity.Email);
        if (existingEmailUser is not null)
        {
            throw new InvalidOperationException(
                "A Fleet account already exists for this email. Sign in with an already-linked method, then link this sign-in method from Security settings.");
        }

        // Auto-provision a new local profile from Entra ID claims
        var user = new UserProfile
        {
            EntraObjectId = currentIdentity.ProviderUserId,
            Username = DeriveUsername(
                IsPlaceholderEmail(currentIdentity.Email) ? string.Empty : currentIdentity.Email,
                currentIdentity.DisplayName),
            Email = currentIdentity.Email,
            DisplayName = currentIdentity.DisplayName,
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
            await EnsureLoginIdentityAsync(user.Id, currentIdentity);
            logger.AuthAutoProvisionedUser(
                user.Id,
                currentIdentity.ProviderUserId.SanitizeForLogging(),
                currentIdentity.Email.SanitizeForLogging());
            _resolvedUserId = user.Id;
            return user;
        }
        catch (DbUpdateException)
        {
            // A concurrent request already created this user (race condition on first login).
            // Re-fetch by linked identity first, then by legacy OID.
            var concurrentIdentity = await authRepository.GetLoginIdentityAsync(currentIdentity.Provider, currentIdentity.ProviderUserId);
            if (concurrentIdentity?.UserProfile is not null)
            {
                await UpdateLoginIdentityUsageAsync(concurrentIdentity, currentIdentity);
                await ApplyRoleUpdatesAsync(concurrentIdentity.UserProfile, isUnlimitedTierUser);
                logger.AuthResolvedUser(currentIdentity.ProviderUserId.SanitizeForLogging(), concurrentIdentity.UserProfileId);
                _resolvedUserId = concurrentIdentity.UserProfileId;
                return concurrentIdentity.UserProfile;
            }

            var concurrentlyCreated = await authRepository.GetByEntraObjectIdAsync(currentIdentity.ProviderUserId);
            if (concurrentlyCreated is not null)
            {
                await EnsureLoginIdentityAsync(concurrentlyCreated.Id, currentIdentity);
                await ApplyRoleUpdatesAsync(concurrentlyCreated, isUnlimitedTierUser);
                logger.AuthResolvedUser(currentIdentity.ProviderUserId.SanitizeForLogging(), concurrentlyCreated.Id);
                _resolvedUserId = concurrentlyCreated.Id;
                return concurrentlyCreated;
            }

            var concurrentlyCreatedByEmail = await authRepository.GetByEmailAsync(currentIdentity.Email);
            if (concurrentlyCreatedByEmail is not null)
            {
                throw new InvalidOperationException(
                    "A Fleet account already exists for this email. Sign in with an already-linked method, then link this sign-in method from Security settings.");
            }

            // If still not found, the constraint violation was for a different reason — rethrow.
            throw;
        }
    }

    private LoginProviderLinkStatePayload ValidateLoginProviderLinkState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new UnauthorizedAccessException("Missing login provider link state.");

        try
        {
            var json = _loginProviderLinkStateProtector.Unprotect(state);
            var payload = JsonSerializer.Deserialize<LoginProviderLinkStatePayload>(json);
            if (payload is null)
                throw new UnauthorizedAccessException("Invalid login provider link state.");

            if (payload.ExpiresAtUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                throw new UnauthorizedAccessException("Login provider link state expired.");

            return payload with { Provider = NormalizeSupportedProvider(payload.Provider) };
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("Invalid login provider link state.", ex);
        }
    }

    private async Task ApplyRoleUpdatesAsync(UserProfile user, bool isUnlimitedTierUser)
    {
        var needsUpdate = false;
        var normalizedRole = UserRoles.Normalize(user.Role);
        if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
        {
            user.Role = normalizedRole;
            needsUpdate = true;
        }

        if (isUnlimitedTierUser && !UserRoles.IsUnlimited(normalizedRole))
        {
            user.Role = UserRoles.Unlimited;
            needsUpdate = true;
        }

        if (needsUpdate)
            await authRepository.UpdateUserAsync(user);
    }

    private async Task EnsureLoginIdentityAsync(int userId, CurrentLoginIdentity currentIdentity)
    {
        var identity = await authRepository.GetLoginIdentityAsync(currentIdentity.Provider, currentIdentity.ProviderUserId);
        if (identity is not null)
        {
            await UpdateLoginIdentityUsageAsync(identity, currentIdentity);
            return;
        }

        try
        {
            await authRepository.CreateLoginIdentityAsync(CreateLoginIdentity(userId, currentIdentity));
        }
        catch (DbUpdateException)
        {
            var concurrentlyCreated = await authRepository.GetLoginIdentityAsync(currentIdentity.Provider, currentIdentity.ProviderUserId);
            if (concurrentlyCreated is null || concurrentlyCreated.UserProfileId != userId)
                throw;
        }
    }

    private async Task UpdateLoginIdentityUsageAsync(LoginIdentity identity, CurrentLoginIdentity currentIdentity)
    {
        var now = DateTime.UtcNow;
        var needsUpdate = false;
        var displayEmail = ToLoginIdentityDisplayEmail(currentIdentity);
        if (!string.Equals(identity.Email, displayEmail, StringComparison.OrdinalIgnoreCase))
        {
            identity.Email = displayEmail;
            needsUpdate = true;
        }

        if (!string.Equals(identity.DisplayName, currentIdentity.DisplayName, StringComparison.Ordinal))
        {
            identity.DisplayName = currentIdentity.DisplayName;
            needsUpdate = true;
        }

        if (identity.LastUsedAtUtc is null ||
            now - identity.LastUsedAtUtc.Value >= LoginIdentityUsageUpdateInterval)
        {
            identity.LastUsedAtUtc = now;
            needsUpdate = true;
        }

        if (needsUpdate)
            await authRepository.UpdateLoginIdentityAsync(identity);
    }

    private CurrentLoginIdentity? TryResolveCurrentLoginIdentity()
    {
        try
        {
            var principal = httpContextAccessor.HttpContext?.User;
            return principal is null ? null : LoginIdentityClaims.Resolve(principal);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static LoginIdentity CreateLoginIdentity(int userId, CurrentLoginIdentity currentIdentity)
        => new()
        {
            UserProfileId = userId,
            Provider = currentIdentity.Provider,
            ProviderUserId = currentIdentity.ProviderUserId,
            Email = ToLoginIdentityDisplayEmail(currentIdentity),
            DisplayName = currentIdentity.DisplayName,
            LinkedAtUtc = DateTime.UtcNow,
            LastUsedAtUtc = DateTime.UtcNow,
        };

    private static LoginIdentityDto ToLoginIdentityDto(
        LoginIdentity identity,
        CurrentLoginIdentity? currentIdentity)
        => new(
            identity.Id,
            identity.Provider,
            ToLoginIdentityDisplayEmail(identity),
            identity.DisplayName,
            identity.LinkedAtUtc,
            identity.LastUsedAtUtc,
            currentIdentity is not null &&
            string.Equals(identity.Provider, currentIdentity.Provider, StringComparison.Ordinal) &&
            string.Equals(identity.ProviderUserId, currentIdentity.ProviderUserId, StringComparison.Ordinal));

    private static string NormalizeSupportedProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Login provider is required.", nameof(provider));

        var normalized = LoginIdentityClaims.NormalizeProvider(provider);
        return normalized is "Email" or "Google" or "Microsoft"
            ? normalized
            : throw new ArgumentException("Unsupported login provider.", nameof(provider));
    }

    private static string DeriveUsername(string email, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            return email.Split('@')[0].ToLowerInvariant();

        return displayName.ToLowerInvariant().Replace(' ', '.');
    }

    private static bool IsPlaceholderEmail(string email)
        => email.EndsWith("@entra.local", StringComparison.OrdinalIgnoreCase);

    private static string? ToLoginIdentityDisplayEmail(CurrentLoginIdentity identity)
        => LoginIdentityClaims.NormalizeDisplayEmail(identity.Email, identity.ProviderUserId);

    private static string? ToLoginIdentityDisplayEmail(LoginIdentity identity)
        => LoginIdentityClaims.NormalizeDisplayEmail(identity.Email, identity.ProviderUserId);

    private static string ToUserProfileDisplayEmail(UserProfile user)
        => LoginIdentityClaims.NormalizeDisplayEmail(user.Email, user.EntraObjectId) ?? string.Empty;

    private static UserProfileDto ToProfileDto(UserProfile user) =>
        new(
            user.DisplayName,
            ToUserProfileDisplayEmail(user),
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

    private sealed record LoginProviderLinkStatePayload(
        int UserId,
        string Provider,
        long ExpiresAtUnix,
        string Nonce);
}
