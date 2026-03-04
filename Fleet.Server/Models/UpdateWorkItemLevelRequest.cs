namespace Fleet.Server.Models;

public record UpdateWorkItemLevelRequest(
    string? Name,
    string? IconName,
    string? Color,
    int? Ordinal
);
