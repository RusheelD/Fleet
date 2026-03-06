namespace Fleet.Server.Subscriptions;

public enum MonthlyRunType
{
    WorkItem,
    Coding,
}

public sealed record TierUsageSnapshot(
    string UtcMonth,
    int WorkItemRunsUsed,
    int CodingRunsUsed,
    int? WorkItemRunsRemaining,
    int? CodingRunsRemaining);

public interface IUsageLedgerService
{
    Task ChargeRunAsync(int userId, MonthlyRunType runType, CancellationToken cancellationToken = default);
    Task RefundRunAsync(int userId, MonthlyRunType runType, CancellationToken cancellationToken = default);
    Task<TierUsageSnapshot> GetCurrentMonthUsageAsync(int userId, CancellationToken cancellationToken = default);
}
