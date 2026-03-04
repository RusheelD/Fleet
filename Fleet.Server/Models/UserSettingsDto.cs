namespace Fleet.Server.Models;

public record UserSettingsDto(
    UserProfileDto Profile,
    LinkedAccountDto[] Connections,
    UserPreferencesDto Preferences
);
