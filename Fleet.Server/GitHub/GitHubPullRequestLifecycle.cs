namespace Fleet.Server.GitHub;

/// <summary>
/// Lightweight lifecycle state for a pull request URL.
/// </summary>
public record GitHubPullRequestLifecycle(
    string PullRequestUrl,
    bool IsOpen,
    bool IsDraft,
    bool IsMerged
);
