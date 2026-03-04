namespace Fleet.Server.GitHub;

/// <summary>
/// Aggregated GitHub statistics for a single repository.
/// </summary>
public record GitHubRepoStats(
    int OpenPullRequests,
    int MergedPullRequests,
    int RecentCommits,
    IReadOnlyList<GitHubActivityEvent> RecentEvents
);
