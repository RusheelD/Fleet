namespace Fleet.Server.Agents;

public static class AgentExecutionDeliveryModes
{
    public const string PullRequest = "pull_request";
    public const string TargetBranch = "target_branch";

    public static string Normalize(string? deliveryMode)
    {
        var normalized = deliveryMode?.Trim();
        return string.Equals(normalized, TargetBranch, StringComparison.OrdinalIgnoreCase)
            ? TargetBranch
            : PullRequest;
    }

    public static bool IsTargetBranch(string? deliveryMode)
        => string.Equals(Normalize(deliveryMode), TargetBranch, StringComparison.OrdinalIgnoreCase);
}
