using Fleet.Server.Auth;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Subscriptions;

public class UsageLedgerService(FleetDbContext dbContext) : IUsageLedgerService
{
    public async Task ChargeRunAsync(
        int userId,
        MonthlyRunType runType,
        CancellationToken cancellationToken = default)
    {
        var policy = TierPolicyCatalog.Get(await GetUserRoleAsync(userId, cancellationToken));
        var ledger = await GetOrCreateCurrentLedgerAsync(userId, cancellationToken);

        var usedBeforeCharge = runType switch
        {
            MonthlyRunType.WorkItem => Math.Max(0, ledger.WorkItemRunCharges - ledger.WorkItemRunRefunds),
            _ => Math.Max(0, ledger.CodingRunCharges - ledger.CodingRunRefunds),
        };

        var limit = runType switch
        {
            MonthlyRunType.WorkItem => policy.MonthlyWorkItemRuns,
            _ => policy.MonthlyCodingRuns,
        };

        if (limit.HasValue && usedBeforeCharge >= limit.Value)
        {
            var runLabel = runType == MonthlyRunType.WorkItem ? "work-item" : "coding";
            throw new InvalidOperationException(
                $"Monthly {runLabel} run limit reached for the '{policy.Tier}' tier ({limit.Value}).");
        }

        switch (runType)
        {
            case MonthlyRunType.WorkItem:
                ledger.WorkItemRunCharges++;
                break;
            default:
                ledger.CodingRunCharges++;
                break;
        }

        ledger.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RefundRunAsync(
        int userId,
        MonthlyRunType runType,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetOrCreateCurrentLedgerAsync(userId, cancellationToken);

        switch (runType)
        {
            case MonthlyRunType.WorkItem when ledger.WorkItemRunCharges > ledger.WorkItemRunRefunds:
                ledger.WorkItemRunRefunds++;
                break;
            case MonthlyRunType.Coding when ledger.CodingRunCharges > ledger.CodingRunRefunds:
                ledger.CodingRunRefunds++;
                break;
        }

        ledger.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TierUsageSnapshot> GetCurrentMonthUsageAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var policy = TierPolicyCatalog.Get(await GetUserRoleAsync(userId, cancellationToken));
        var ledger = await GetOrCreateCurrentLedgerAsync(userId, cancellationToken);

        var workItemUsed = Math.Max(0, ledger.WorkItemRunCharges - ledger.WorkItemRunRefunds);
        var codingUsed = Math.Max(0, ledger.CodingRunCharges - ledger.CodingRunRefunds);

        var workItemRemaining = policy.MonthlyWorkItemRuns.HasValue
            ? Math.Max(policy.MonthlyWorkItemRuns.Value - workItemUsed, 0)
            : (int?)null;
        var codingRemaining = policy.MonthlyCodingRuns.HasValue
            ? Math.Max(policy.MonthlyCodingRuns.Value - codingUsed, 0)
            : (int?)null;

        return new TierUsageSnapshot(
            ledger.UtcMonth,
            workItemUsed,
            codingUsed,
            workItemRemaining,
            codingRemaining,
            ledger.InputTokens,
            ledger.OutputTokens,
            ledger.CachedInputTokens);
    }

    private async Task<MonthlyUsageLedger> GetOrCreateCurrentLedgerAsync(int userId, CancellationToken cancellationToken)
    {
        var utcMonth = GetUtcMonthKey();
        var existing = await dbContext.MonthlyUsageLedgers
            .FirstOrDefaultAsync(x => x.UserProfileId == userId && x.UtcMonth == utcMonth, cancellationToken);

        if (existing is not null)
            return existing;

        var created = new MonthlyUsageLedger
        {
            UserProfileId = userId,
            UtcMonth = utcMonth,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        dbContext.MonthlyUsageLedgers.Add(created);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            // Concurrent create race: re-load the single row for this user/month.
            var reloaded = await dbContext.MonthlyUsageLedgers
                .FirstOrDefaultAsync(x => x.UserProfileId == userId && x.UtcMonth == utcMonth, cancellationToken);
            if (reloaded is not null)
                return reloaded;

            throw;
        }
    }

    private static string GetUtcMonthKey()
        => DateTime.UtcNow.ToString("yyyy-MM");

    private async Task<string> GetUserRoleAsync(int userId, CancellationToken cancellationToken)
    {
        var role = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (role is null)
            throw new InvalidOperationException("User profile not found.");

        return UserRoles.Normalize(role);
    }

    public async Task RecordTokensAsync(
        int userId,
        int inputTokens,
        int outputTokens,
        int cachedTokens = 0,
        CancellationToken cancellationToken = default)
    {
        var ledger = await GetOrCreateCurrentLedgerAsync(userId, cancellationToken);
        ledger.InputTokens += inputTokens;
        ledger.OutputTokens += outputTokens;
        ledger.CachedInputTokens += cachedTokens;
        ledger.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
