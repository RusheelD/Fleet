namespace Fleet.Server.Models;

public record UpdateProfileRequest(
    string DisplayName,
    string Email,
    string Bio,
    string Location
);
