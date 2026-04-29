namespace Fleet.Server.Models;

public static class ChatSessionBranchStrategy
{
    public const string SessionPinnedBranch = "SessionPinnedBranch";
    public const string PerWorkItemPattern = "PerWorkItemPattern";
    public const string AutoFromProjectPattern = "AutoFromProjectPattern";

    public static string Normalize(string? value)
        => value switch
        {
            SessionPinnedBranch => SessionPinnedBranch,
            PerWorkItemPattern => PerWorkItemPattern,
            AutoFromProjectPattern => AutoFromProjectPattern,
            _ => AutoFromProjectPattern,
        };
}
