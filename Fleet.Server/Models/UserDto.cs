namespace Fleet.Server.Models;

public record UserProfileDto(
    string DisplayName,
    string Email,
    string Bio,
    string Location,
    string AvatarUrl,
    string Role = Auth.UserRoles.Free
);
