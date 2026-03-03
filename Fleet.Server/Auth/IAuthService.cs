using Fleet.Server.Models;

namespace Fleet.Server.Auth;

public interface IAuthService
{
    Task<UserProfileDto> GetOrCreateCurrentUserAsync();
    Task<int> GetCurrentUserIdAsync();
}
