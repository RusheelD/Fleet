using Fleet.Server.Models;

namespace Fleet.Server.Users;

public interface IUserService
{
    Task<UserSettingsDto> GetSettingsAsync(int userId);
    Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UserPreferencesDto preferences);
}
