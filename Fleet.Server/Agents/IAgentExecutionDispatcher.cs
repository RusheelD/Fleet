namespace Fleet.Server.Agents;

public interface IAgentExecutionDispatcher
{
    Task<string> DispatchWorkItemAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? requestedTargetBranch = null,
        string? chatSessionId = null,
        string? parentExecutionId = null,
        CancellationToken cancellationToken = default);

    Task<string> DispatchWorkItemToTargetBranchAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? requestedTargetBranch = null,
        string? chatSessionId = null,
        string? parentExecutionId = null,
        CancellationToken cancellationToken = default);
}
