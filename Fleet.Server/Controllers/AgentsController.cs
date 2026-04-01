using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId}/agents")]
[ServiceFilter(typeof(ProjectOwnershipFilter))]
public class AgentsController(
    IAgentService agentService,
    IAgentOrchestrationService orchestrationService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    [HttpGet("executions")]
    public async Task<IActionResult> GetExecutions(string projectId)
    {
        var executions = await agentService.GetExecutionsAsync(projectId);
        return Ok(executions);
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(string projectId)
    {
        var logs = await agentService.GetLogsAsync(projectId);
        return Ok(logs);
    }

    [HttpDelete("logs")]
    public async Task<IActionResult> ClearLogs(string projectId)
    {
        var deletedCount = await agentService.ClearLogsAsync(projectId);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.LogsUpdated,
            new { projectId, deletedCount });
        return Ok(new { deletedCount });
    }

    [HttpDelete("executions/{executionId}/logs")]
    public async Task<IActionResult> ClearExecutionLogs(string projectId, string executionId)
    {
        var deletedCount = await agentService.ClearExecutionLogsAsync(projectId, executionId);
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.LogsUpdated,
            new { projectId, executionId, deletedCount });
        return Ok(new { executionId, deletedCount });
    }

    /// <summary>
    /// Starts an agent execution pipeline for a work item.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> StartExecution(string projectId, [FromBody] StartExecutionRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var executionId = await orchestrationService.StartExecutionAsync(
            projectId, request.WorkItemNumber, userId, request.TargetBranch);

        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.AgentsUpdated,
            new { projectId, executionId });
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId, workItemNumber = request.WorkItemNumber });
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ProjectsUpdated,
            new { projectId });

        return Accepted(new { executionId });
    }

    /// <summary>
    /// Gets the status of a running or completed execution.
    /// </summary>
    [HttpGet("executions/{executionId}/status")]
    public async Task<IActionResult> GetExecutionStatus(string projectId, string executionId)
    {
        var status = await orchestrationService.GetExecutionStatusAsync(projectId, executionId);
        if (status is null) return NotFound();
        return Ok(status);
    }

    /// <summary>
    /// Cancels (stops) a running execution.
    /// </summary>
    [HttpPost("executions/{executionId}/cancel")]
    public async Task<IActionResult> CancelExecution(string projectId, string executionId)
    {
        var cancelled = await orchestrationService.CancelExecutionAsync(projectId, executionId);
        if (!cancelled) return NotFound();
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.AgentsUpdated,
            new { projectId, executionId, status = "cancelled" });
        return Ok(new { executionId, status = "cancelled" });
    }

    /// <summary>
    /// Pauses a running execution.
    /// </summary>
    [HttpPost("executions/{executionId}/pause")]
    public async Task<IActionResult> PauseExecution(string projectId, string executionId)
    {
        var paused = await orchestrationService.PauseExecutionAsync(projectId, executionId);
        if (!paused) return NotFound();
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.AgentsUpdated,
            new { projectId, executionId, status = "paused" });
        return Ok(new { executionId, status = "paused" });
    }

    /// <summary>
    /// Adds steering guidance for an active execution.
    /// </summary>
    [HttpPost("executions/{executionId}/steer")]
    public async Task<IActionResult> SteerExecution(string projectId, string executionId, [FromBody] SteerExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
            return BadRequest("A non-empty steering note is required.");

        var accepted = await orchestrationService.SteerExecutionAsync(projectId, executionId, request.Note);
        if (!accepted) return NotFound();
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.AgentsUpdated,
            new { projectId, executionId, status = "steering_note_added" });
        return Accepted(new { executionId, status = "steering_note_added" });
    }

    /// <summary>
    /// Retries a stopped execution by starting a new run for the same work item.
    /// </summary>
    [HttpPost("executions/{executionId}/retry")]
    public async Task<IActionResult> RetryExecution(string projectId, string executionId)
    {
        var status = await orchestrationService.GetExecutionStatusAsync(projectId, executionId);
        if (status is null)
            return NotFound();

        if (string.Equals(status.Status, "running", StringComparison.OrdinalIgnoreCase))
            return Conflict("Execution is still running and cannot be retried.");

        var userId = await authService.GetCurrentUserIdAsync();
        var newExecutionId = await orchestrationService.RetryExecutionAsync(projectId, executionId, userId);
        if (newExecutionId is null)
            return NotFound();

        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.AgentsUpdated,
            new { projectId, executionId = newExecutionId, retriedFromExecutionId = executionId });
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.WorkItemsUpdated,
            new { projectId });
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ProjectsUpdated,
            new { projectId });

        return Accepted(new { executionId = newExecutionId });
    }

    [HttpDelete("executions/{executionId}")]
    public async Task<IActionResult> DeleteExecution(string projectId, string executionId)
    {
        AgentExecutionDeletionResult? result;
        try
        {
            result = await orchestrationService.DeleteExecutionAsync(projectId, executionId);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        if (result is null)
            return NotFound();

        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.AgentsUpdated,
            new { projectId, executionId, status = "deleted" });
        await eventPublisher.PublishProjectEventAsync(
            userId,
            projectId,
            ServerEventTopics.LogsUpdated,
            new { projectId, executionId, deletedCount = result.DeletedLogCount });

        return Ok(result);
    }

    /// <summary>
    /// Returns generated documentation for an execution.
    /// </summary>
    [HttpGet("executions/{executionId}/docs")]
    public async Task<IActionResult> GetExecutionDocumentation(string projectId, string executionId)
    {
        var docs = await orchestrationService.GetExecutionDocumentationAsync(projectId, executionId);
        if (docs is null)
            return NotFound();

        return Ok(docs);
    }
}
