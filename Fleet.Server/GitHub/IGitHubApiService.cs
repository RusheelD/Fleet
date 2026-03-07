namespace Fleet.Server.GitHub;

/// <summary>
/// Fetches repository statistics (PRs, commits, recent events) from the GitHub API.
/// </summary>
public interface IGitHubApiService
{
    Task<GitHubRepoStats> GetRepoStatsAsync(int userId, string repoFullName);
    Task<IReadOnlyList<GitHubWorkItemReference>> GetWorkItemReferencesAsync(int userId, string repoFullName);
    Task<GitHubPullRequestLifecycle?> GetPullRequestLifecycleByUrlAsync(int userId, string pullRequestUrl);
}
