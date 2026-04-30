namespace Fleet.Server.GitHub;

public sealed record GitHubBranchInfo(
    string Name,
    bool IsDefault,
    bool IsProtected);
