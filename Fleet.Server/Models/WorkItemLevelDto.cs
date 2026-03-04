namespace Fleet.Server.Models;

public record WorkItemLevelDto(
    int Id,
    string Name,
    string IconName,
    string Color,
    int Ordinal,
    bool IsDefault
);
