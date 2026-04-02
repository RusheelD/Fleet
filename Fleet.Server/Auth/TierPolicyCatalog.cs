namespace Fleet.Server.Auth;

public sealed record TierPolicy(
    string Tier,
    int? MonthlyWorkItemRuns,
    int? MonthlyCodingRuns,
    int RequestsPerMinute,
    int MaxConcurrentAgentsPerTask,
    int MaxActiveAgentExecutions,
    bool UnlimitedRateLimit = false);

public static class TierPolicyCatalog
{
    private static TierPolicy CreateTemporaryFullAccessPolicy(string tier) => new(
        Tier: tier,
        MonthlyWorkItemRuns: null,
        MonthlyCodingRuns: null,
        RequestsPerMinute: int.MaxValue,
        MaxConcurrentAgentsPerTask: int.MaxValue,
        MaxActiveAgentExecutions: int.MaxValue,
        UnlimitedRateLimit: true);

    private static readonly IReadOnlyDictionary<string, TierPolicy> Policies =
        new Dictionary<string, TierPolicy>(StringComparer.OrdinalIgnoreCase)
        {
            // Temporary product decision: free users receive the same effective access
            // as unlimited users while the pricing model is still in flux.
            [UserRoles.Free] = CreateTemporaryFullAccessPolicy(UserRoles.Free),
            [UserRoles.Basic] = new(
                Tier: UserRoles.Basic,
                MonthlyWorkItemRuns: 100,
                MonthlyCodingRuns: 30,
                RequestsPerMinute: 300,
                MaxConcurrentAgentsPerTask: 2,
                MaxActiveAgentExecutions: 3),
            [UserRoles.Pro] = new(
                Tier: UserRoles.Pro,
                MonthlyWorkItemRuns: 500,
                MonthlyCodingRuns: 200,
                RequestsPerMinute: 600,
                MaxConcurrentAgentsPerTask: 4,
                MaxActiveAgentExecutions: 10),
            [UserRoles.Unlimited] = CreateTemporaryFullAccessPolicy(UserRoles.Unlimited),
        };

    public static TierPolicy Get(string? role)
    {
        var normalized = UserRoles.Normalize(role);
        return Policies.TryGetValue(normalized, out var policy)
            ? policy
            : Policies[UserRoles.Free];
    }

    public static IReadOnlyList<TierPolicy> All => Policies.Values.ToList();
}
