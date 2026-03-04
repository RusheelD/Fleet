namespace Fleet.Server.Models;

public record CreateWorkItemLevelRequest(
    string Name,
    string IconName,
    string Color,
    int Ordinal
);
