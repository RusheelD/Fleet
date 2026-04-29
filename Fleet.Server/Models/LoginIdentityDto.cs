namespace Fleet.Server.Models;

public record LoginIdentityDto(
    int Id,
    string Provider,
    string? Email,
    string? DisplayName,
    DateTime LinkedAtUtc,
    DateTime? LastUsedAtUtc,
    bool IsCurrent = false);

public record LoginProviderLinkStateDto(string State);

public record CreateLoginProviderLinkRequest(string Provider);

public record CompleteLoginProviderLinkRequest(string State);
