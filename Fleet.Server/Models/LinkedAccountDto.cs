namespace Fleet.Server.Models;

public record LinkedAccountDto(
    int Id,
    string Provider,
    string? ConnectedAs,
    string? ExternalUserId = null,
    DateTime? ConnectedAt = null,
    bool IsPrimary = false
);
