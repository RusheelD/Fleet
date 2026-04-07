namespace Fleet.Server.Models;

public record UpsertMemoryEntryRequest(
    string Name,
    string Description,
    string Type,
    string Content,
    bool AlwaysInclude
);
