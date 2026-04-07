namespace Fleet.Server.Models;

public record MemoryEntryDto(
    int Id,
    string Name,
    string Description,
    string Type,
    string Content,
    bool AlwaysInclude,
    string Scope,
    string? ProjectId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool IsStale,
    string? StalenessMessage
);
