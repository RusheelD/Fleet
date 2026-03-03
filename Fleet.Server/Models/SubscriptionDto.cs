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

public record UsageMeterDto(
    string Label,
    string Usage,
    double Value,
    string Color,
    string Remaining
);

public record CurrentPlanDto(
    string Name,
    string Description
);

public record SubscriptionDataDto(
    CurrentPlanDto CurrentPlan,
    UsageMeterDto[] Usage,
    PlanDto[] Plans
);
