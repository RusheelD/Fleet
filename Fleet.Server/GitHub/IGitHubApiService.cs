namespace Fleet.Server.GitHub;

/// <summary>
/// Fetches repository statistics (PRs, commits, recent events) from the GitHub API.
/// </summary>
public interface IGitHubApiService
{
    Task<GitHubRepoStats> GetRepoStatsAsync(int userId, string repoFullName);
}

/// <summary>
/// Aggregated GitHub statistics for a single repository.
/// </summary>
public record GitHubRepoStats(
    int OpenPullRequests,
    int MergedPullRequests,
    int RecentCommits,
    IReadOnlyList<GitHubActivityEvent> RecentEvents
);

/// <summary>
/// A simplified recent activity event from GitHub.
/// </summary>
public record GitHubActivityEvent(
    string Icon,
    string Text,
    DateTimeOffset Timestamp
);
