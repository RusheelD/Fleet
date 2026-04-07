using Fleet.Server.Models;
using Fleet.Server.Logging;
using Fleet.Server.Auth;
using Fleet.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Subscriptions;

public class SubscriptionService(
    IUsageLedgerService usageLedgerService,
    FleetDbContext dbContext,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public async Task<SubscriptionDataDto> GetSubscriptionDataAsync(int userId)
    {
        logger.SubscriptionsRetrieving();

        var role = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();

        var normalizedRole = UserRoles.Normalize(role);
        var policy = TierPolicyCatalog.Get(normalizedRole);
        var usage = await usageLedgerService.GetCurrentMonthUsageAsync(userId);

        var tierLabel = $"{char.ToUpper(normalizedRole[0])}{normalizedRole[1..]}";
        var currentPlan = new CurrentPlanDto(
            $"{tierLabel} Tier",
            policy.MonthlyWorkItemRuns.HasValue
                ? $"Monthly limits: {policy.MonthlyWorkItemRuns.Value} work-item runs, {policy.MonthlyCodingRuns!.Value} coding runs."
                : "Unlimited usage. No monthly run caps.");

        var usageMeters = new[]
        {
            BuildRunUsageMeter("Work-Item Runs", usage.WorkItemRunsUsed, policy.MonthlyWorkItemRuns, usage.WorkItemRunsRemaining),
            BuildRunUsageMeter("Coding Runs", usage.CodingRunsUsed, policy.MonthlyCodingRuns, usage.CodingRunsRemaining),
            new UsageMeterDto(
                "API Rate Limit",
                policy.UnlimitedRateLimit ? "Unlimited" : $"{policy.RequestsPerMinute}/min",
                policy.UnlimitedRateLimit ? 0 : 0.5,
                "brand",
                policy.UnlimitedRateLimit ? "No API throttling" : "Global per-user fixed window"),
            new UsageMeterDto(
                "Concurrent Agents / Task",
                policy.MaxConcurrentAgentsPerTask == int.MaxValue
                    ? "Unlimited"
                    : policy.MaxConcurrentAgentsPerTask.ToString(),
                0,
                "brand",
                "Tier concurrency cap"),
            new UsageMeterDto(
                "Active Executions",
                policy.MaxActiveAgentExecutions == int.MaxValue
                    ? "Unlimited"
                    : policy.MaxActiveAgentExecutions.ToString(),
                0,
                "brand",
                $"Window: {usage.UtcMonth} UTC"),
            BuildTokenUsageMeter("Input Tokens", usage.InputTokensUsed),
            BuildTokenUsageMeter("Output Tokens", usage.OutputTokensUsed),
            BuildTokenUsageMeter("Cached Tokens", usage.CachedInputTokens),
        };

        var plans = TierPolicyCatalog.All
            .Select(t => ToPlan(t, normalizedRole))
            .ToArray();

        return new SubscriptionDataDto(currentPlan, usageMeters, plans);
    }

    private static UsageMeterDto BuildRunUsageMeter(string label, int used, int? limit, int? remaining)
    {
        if (!limit.HasValue)
        {
            return new UsageMeterDto(
                label,
                $"{used} used",
                0,
                "brand",
                "Unlimited");
        }

        var clampedUsed = Math.Min(used, limit.Value);
        var value = limit.Value == 0 ? 0 : (double)clampedUsed / limit.Value;
        var color = value >= 1 ? "warning" : "brand";

        return new UsageMeterDto(
            label,
            $"{clampedUsed} / {limit.Value}",
            value,
            color,
            $"{remaining ?? Math.Max(limit.Value - clampedUsed, 0)} remaining");
    }

    private static UsageMeterDto BuildTokenUsageMeter(string label, long tokens)
    {
        var formatted = tokens switch
        {
            >= 1_000_000 => $"{tokens / 1_000_000.0:F1}M",
            >= 1_000 => $"{tokens / 1_000.0:F1}K",
            _ => tokens.ToString(),
        };

        return new UsageMeterDto(label, formatted, 0, "brand", "This month");
    }

    private static PlanDto ToPlan(TierPolicy policy, string currentRole)
    {
        var isCurrent = string.Equals(policy.Tier, currentRole, StringComparison.OrdinalIgnoreCase);
        var titleCaseTier = $"{char.ToUpper(policy.Tier[0])}{policy.Tier[1..]}";

        return new PlanDto(
            Name: titleCaseTier,
            Icon: policy.Tier switch
            {
                UserRoles.Free => "rocket",
                UserRoles.Basic => "diamond",
                UserRoles.Pro => "sparkle",
                _ => "flash",
            },
            Price: policy.Tier switch
            {
                UserRoles.Free => "$0",
                UserRoles.Basic => "$200",
                UserRoles.Pro => "$1000",
                _ => "$5000",
            },
            Period: "/month",
            Description: policy.UnlimitedRateLimit
                ? "No API throttling and no monthly run caps."
                : "MVP tier with enforced monthly run and concurrency limits.",
            Features:
            [
                policy.MonthlyWorkItemRuns.HasValue
                    ? $"{policy.MonthlyWorkItemRuns.Value} work-item runs / month"
                    : "Unlimited work-item runs",
                policy.MonthlyCodingRuns.HasValue
                    ? $"{policy.MonthlyCodingRuns.Value} coding runs / month"
                    : "Unlimited coding runs",
                policy.UnlimitedRateLimit ? "Unlimited API rate" : $"{policy.RequestsPerMinute} requests / minute",
                policy.MaxConcurrentAgentsPerTask == int.MaxValue
                    ? "Unlimited concurrent agents / task"
                    : $"{policy.MaxConcurrentAgentsPerTask} concurrent agents / task",
                policy.MaxActiveAgentExecutions == int.MaxValue
                    ? "Unlimited active executions"
                    : $"{policy.MaxActiveAgentExecutions} active executions",
            ],
            ButtonLabel: isCurrent ? "Current Tier" : "Admin Managed",
            IsCurrent: isCurrent,
            ButtonAppearance: isCurrent ? "outline" : "primary");
    }
}
