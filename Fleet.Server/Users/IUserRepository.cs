using Fleet.Server.Models;

namespace Fleet.Server.Users;

public interface IUserRepository
{
    Task<UserProfileDto?> GetProfileAsync(int userId);
    Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId);
    Task<UserPreferencesDto?> GetPreferencesAsync(int userId);
    Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UserPreferencesDto preferences);
}
