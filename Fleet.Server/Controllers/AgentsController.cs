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
        var status = await orchestrationService.GetExecutionStatusAsync(executionId);
        if (status is null) return NotFound();
        return Ok(status);
    }
}
