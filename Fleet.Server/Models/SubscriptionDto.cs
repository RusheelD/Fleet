namespace Fleet.Server.Models;

public record PlanDto(
    string Name,
    string Icon,
    string Price,
    string Period,
    string Description,
    string[] Features,
    string ButtonLabel,
    bool IsCurrent,
    string ButtonAppearance
);
