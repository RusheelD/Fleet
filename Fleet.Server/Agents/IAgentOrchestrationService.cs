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
    /// Gets the current status of an execution.
    /// </summary>
    Task<AgentExecutionStatus?> GetExecutionStatusAsync(string executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active execution. The pipeline stops and the execution status is set to "cancelled".
    /// </summary>
    /// <returns>True if the execution was found and cancellation was signalled; false if not found or already stopped.</returns>
    Task<bool> CancelExecutionAsync(string executionId);

    /// <summary>
    /// Pauses an active execution. The pipeline stops and the execution status is set to "paused".
    /// </summary>
    /// <returns>True if the execution was found and pause was signalled; false if not found or already stopped.</returns>
    Task<bool> PauseExecutionAsync(string executionId);
}
