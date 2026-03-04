namespace Fleet.Server.Models;

public record SubscriptionDataDto(
    CurrentPlanDto CurrentPlan,
    UsageMeterDto[] Usage,
    PlanDto[] Plans
);
