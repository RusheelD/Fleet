using Fleet.Server.Models;

namespace Fleet.Server.Users;

public class UserService(
    IUserRepository userRepository,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserSettingsDto> GetSettingsAsync(int userId)
    {
        logger.LogInformation("Retrieving user settings for user {UserId}", userId);
        var profile = await userRepository.GetProfileAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        var connections = await userRepository.GetConnectionsAsync(userId);
        var preferences = await userRepository.GetPreferencesAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        return new UserSettingsDto(profile, [.. connections], preferences);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        logger.LogInformation("Updating user profile for user {UserId}", userId);
        return await userRepository.UpdateProfileAsync(userId, request);
    }

    public async Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UserPreferencesDto preferences)
    {
        logger.LogInformation("Updating user preferences for user {UserId}", userId);
        return await userRepository.UpdatePreferencesAsync(userId, preferences);
    }
}
