namespace Fleet.Server.GitHub;

/// <summary>
/// A simplified recent activity event from GitHub.
/// </summary>
public record GitHubActivityEvent(
    string Icon,
    string Text,
    DateTimeOffset Timestamp
);
