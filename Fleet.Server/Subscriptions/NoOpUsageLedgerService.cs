namespace Fleet.Server.Subscriptions;

public sealed class NoOpUsageLedgerService : IUsageLedgerService
{
    public static NoOpUsageLedgerService Instance { get; } = new();

    private NoOpUsageLedgerService()
    {
    }

    public Task ChargeRunAsync(int userId, MonthlyRunType runType, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RefundRunAsync(int userId, MonthlyRunType runType, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<TierUsageSnapshot> GetCurrentMonthUsageAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new TierUsageSnapshot(
            UtcMonth: DateTime.UtcNow.ToString("yyyy-MM"),
            WorkItemRunsUsed: 0,
            CodingRunsUsed: 0,
            WorkItemRunsRemaining: null,
            CodingRunsRemaining: null));

    public Task RecordTokensAsync(int userId, int inputTokens, int outputTokens, int cachedTokens = 0, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
