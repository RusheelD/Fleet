using Fleet.Server.Models;

namespace Fleet.Server.Auth;

public interface IAuthService
{
    Task<UserProfileDto> GetOrCreateCurrentUserAsync();
    Task<int> GetCurrentUserIdAsync();
    Task<LoginProviderLinkStateDto> CreateLoginProviderLinkStateAsync(int userId, string provider);
    Task<UserProfileDto> CompleteLoginProviderLinkAsync(string state);
    Task<IReadOnlyList<LoginIdentityDto>> GetLoginIdentitiesAsync(int userId);
    Task DeleteLoginIdentityAsync(int userId, int identityId);
}
