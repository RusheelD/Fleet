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
    int? CodingRunsRemaining,
    long InputTokensUsed = 0,
    long OutputTokensUsed = 0,
    long CachedInputTokens = 0);

public interface IUsageLedgerService
{
    Task ChargeRunAsync(int userId, MonthlyRunType runType, CancellationToken cancellationToken = default);
    Task RefundRunAsync(int userId, MonthlyRunType runType, CancellationToken cancellationToken = default);
    Task<TierUsageSnapshot> GetCurrentMonthUsageAsync(int userId, CancellationToken cancellationToken = default);
    Task RecordTokensAsync(int userId, int inputTokens, int outputTokens, int cachedTokens = 0, CancellationToken cancellationToken = default);
}
