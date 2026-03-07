namespace Fleet.Server.GitHub;

/// <summary>
/// Represents a work-item reference found in PR text, such as "F#123".
/// </summary>
public record GitHubWorkItemReference(
    int WorkItemNumber,
    string PullRequestUrl,
    string PullRequestTitle,
    bool IsFixReference,
    bool IsMerged,
    DateTimeOffset UpdatedAt,
    bool IsOpen = false,
    bool IsDraft = false
);
