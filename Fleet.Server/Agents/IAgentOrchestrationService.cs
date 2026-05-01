using Fleet.Server.Models;

namespace Fleet.Server.Agents;

/// <summary>
/// Orchestrates the full agent execution pipeline for a work item.
/// Coordinates sequential phase execution, context passing, and lifecycle management.
/// </summary>
public interface IAgentOrchestrationService
{
    /// <summary>
    /// Starts a new agent execution for the given work item.
    /// Runs the full sequential pipeline (Planner → Contracts → Implementation → Consolidation → Review).
    /// </summary>
    /// <param name="projectId">The project containing the work item.</param>
    /// <param name="workItemNumber">The work item number to execute.</param>
    /// <param name="userId">The authenticated user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution ID.</returns>
    Task<string> StartExecutionAsync(string projectId, int workItemNumber, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new execution and optionally targets a specific PR base branch.
    /// </summary>
    /// <param name="targetBranch">Optional PR target branch (for example: "main", "develop", "release/v1").</param>
    Task<string> StartExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? targetBranch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new execution with an explicit delivery mode.
    /// Target-branch delivery writes the completed result directly to <paramref name="targetBranch"/> without opening a PR.
    /// </summary>
    Task<string> StartExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string? targetBranch,
        string? deliveryMode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an internal child execution that belongs to a parent flow. This does not consume an additional run.
    /// </summary>
    Task<string> StartSubFlowExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string parentExecutionId,
        string? targetBranch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an internal child execution with an explicit delivery mode.
    /// </summary>
    Task<string> StartSubFlowExecutionAsync(
        string projectId,
        int workItemNumber,
        int userId,
        string parentExecutionId,
        string? targetBranch,
        string? deliveryMode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of an execution.
    /// </summary>
    Task<AgentExecutionStatus?> GetExecutionStatusAsync(string executionId, CancellationToken cancellationToken = default);
    Task<AgentExecutionStatus?> GetExecutionStatusAsync(string projectId, string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active execution. The pipeline stops and the execution status is set to "cancelled".
    /// </summary>
    /// <returns>True if the execution was found and cancellation was signalled; false if not found or already stopped.</returns>
    Task<bool> CancelExecutionAsync(string executionId);
    Task<bool> CancelExecutionAsync(string projectId, string executionId);

    /// <summary>
    /// Pauses an active execution. The pipeline stops and the execution status is set to "paused".
    /// </summary>
    /// <returns>True if the execution was found and pause was signalled; false if not found or already stopped.</returns>
    Task<bool> PauseExecutionAsync(string executionId);
    Task<bool> PauseExecutionAsync(string projectId, string executionId);

    /// <summary>
    /// Resumes a paused execution in place using the same execution id, branch, and PR when available.
    /// </summary>
    Task<bool> ResumeExecutionAsync(string projectId, string executionId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers an interrupted execution that was still marked as running or queued when the service restarted.
    /// </summary>
    Task<bool> RecoverExecutionAsync(string projectId, string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers top-level interrupted executions after a service restart.
    /// </summary>
    Task<int> RecoverInterruptedExecutionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a steering note for an in-flight execution.
    /// </summary>
    Task<bool> SteerExecutionAsync(string projectId, string executionId, string note);

    /// <summary>
    /// Retries a previous execution by starting a new execution for the same work item.
    /// </summary>
    Task<string?> RetryExecutionAsync(string projectId, string executionId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an execution and any logs associated with it. Active executions are cancelled and suppressed before removal.
    /// </summary>
    Task<AgentExecutionDeletionResult?> DeleteExecutionAsync(string projectId, string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds documentation markdown for an execution from persisted phase results.
    /// </summary>
    Task<ExecutionDocumentationDto?> GetExecutionDocumentationAsync(string projectId, string executionId, CancellationToken cancellationToken = default);
}
