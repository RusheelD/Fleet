using Fleet.Server.Data;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Subscriptions;

public class SubscriptionRepository(FleetDbContext context) : ISubscriptionRepository
{
    public async Task<SubscriptionDataDto> GetSubscriptionDataAsync()
    {
        var sub = await context.Subscriptions.FirstOrDefaultAsync();
        if (sub is null)
        {
            return new SubscriptionDataDto(
                new CurrentPlanDto("Free Plan", "No subscription data found."),
                [],
                []
            );
        }

        return new SubscriptionDataDto(
            new CurrentPlanDto(sub.CurrentPlanName, sub.CurrentPlanDescription),
            sub.UsageMeters.Select(m => new UsageMeterDto(m.Label, m.Usage, m.Value, m.Color, m.Remaining)).ToArray(),
            sub.Plans.Select(p => new PlanDto(
                p.Name, p.Icon, p.Price, p.Period, p.Description,
                [.. p.Features], p.ButtonLabel, p.IsCurrent, p.ButtonAppearance
            )).ToArray()
        );
    }
}
