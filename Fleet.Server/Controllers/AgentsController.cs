using Fleet.Server.Agents;
using Fleet.Server.Auth;
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
    IAuthService authService) : ControllerBase
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

    /// <summary>
    /// Starts an agent execution pipeline for a work item.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> StartExecution(string projectId, [FromBody] StartExecutionRequest request)
    {
        var userId = await authService.GetCurrentUserIdAsync();
        var executionId = await orchestrationService.StartExecutionAsync(
            projectId, request.WorkItemNumber, userId);

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

        return Accepted(new { executionId = newExecutionId });
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
