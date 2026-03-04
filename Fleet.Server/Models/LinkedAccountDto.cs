namespace Fleet.Server.Models;

public record LinkedAccountDto(
    string Provider,
    string? ConnectedAs,
    string? ExternalUserId = null,
    DateTime? ConnectedAt = null
);
