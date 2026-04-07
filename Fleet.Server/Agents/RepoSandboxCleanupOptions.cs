using Microsoft.Extensions.Configuration;

namespace Fleet.Server.Agents;

public sealed class RepoSandboxCleanupOptions
{
    public const string SectionName = "RepoSandboxCleanup";

    public string RootPath { get; set; } = RepoSandboxOptions.GetDefaultRootPath();

    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromHours(12);

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    public static RepoSandboxCleanupOptions Resolve(IConfiguration configuration, string defaultRootPath)
    {
        var options = new RepoSandboxCleanupOptions
        {
            RootPath = defaultRootPath,
        };

        var staleAfterHours = configuration.GetValue<double?>($"{SectionName}:StaleAfterHours")
            ?? configuration.GetValue<double?>("REPO_SANDBOX_STALE_AFTER_HOURS");
        if (staleAfterHours is > 0)
            options.StaleAfter = TimeSpan.FromHours(staleAfterHours.Value);

        var cleanupIntervalMinutes = configuration.GetValue<double?>($"{SectionName}:IntervalMinutes")
            ?? configuration.GetValue<double?>("REPO_SANDBOX_CLEANUP_INTERVAL_MINUTES");
        if (cleanupIntervalMinutes is > 0)
            options.Interval = TimeSpan.FromMinutes(cleanupIntervalMinutes.Value);

        return options;
    }
}
