namespace Fleet.Server.Agents;

internal interface IAgentExecutionPipelineRunner
{
    Task RunExecutionPipelineAsync(
        ExecutionPipelineLaunchRequest request,
        CancellationToken cancellationToken);
}

internal sealed record ExecutionPipelineLaunchRequest(
    string ExecutionId,
    string ProjectId,
    Models.WorkItemDto WorkItem,
    List<Models.WorkItemDto> ChildWorkItems,
    string RepoFullName,
    string BranchName,
    string CommitAuthorName,
    string CommitAuthorEmail,
    int UserId,
    string SelectedModelKey,
    AgentRole[][] Pipeline,
    int MaxConcurrentAgentsPerTask,
    string PullRequestTargetBranch,
    int ExistingPullRequestNumber,
    bool BillableExecution,
    string? ParentExecutionId,
    string? RetrySourceExecutionId,
    string? RetrySourceStatus,
    string? RetryReuseBranchName,
    string? RetryReusePullRequestUrl,
    int? RetryReusePullRequestNumber,
    string? RetryReusePullRequestTitle,
    double RetryPriorProgressEstimate,
    IReadOnlyDictionary<AgentRole, string>? RetryCarryForwardOutputs,
    IReadOnlyList<string>? RetryLineageExecutionIds,
    string? RetryContextMarkdown,
    bool RetryResumeInPlace,
    bool RetryResumeFromRemoteBranch);
