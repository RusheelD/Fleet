using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class AgentsControllerTests
{
    private Mock<IAgentService> _agentService = null!;
    private Mock<IAgentOrchestrationService> _orchestrationService = null!;
    private Mock<IAuthService> _authService = null!;
    private AgentsController _sut = null!;

    private const string ProjectId = "proj-1";
    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _agentService = new Mock<IAgentService>();
        _orchestrationService = new Mock<IAgentOrchestrationService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new AgentsController(_agentService.Object, _orchestrationService.Object, _authService.Object);
    }

    [TestMethod]
    public async Task GetExecutions_ReturnsOk()
    {
        _agentService.Setup(s => s.GetExecutionsAsync(ProjectId))
            .ReturnsAsync(new List<AgentExecutionDto>());

        var result = await _sut.GetExecutions(ProjectId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetLogs_ReturnsOk()
    {
        _agentService.Setup(s => s.GetLogsAsync(ProjectId))
            .ReturnsAsync(new List<LogEntryDto>());

        var result = await _sut.GetLogs(ProjectId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task StartExecution_ReturnsAccepted()
    {
        _orchestrationService
            .Setup(s => s.StartExecutionAsync(ProjectId, 1, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-123");

        var result = await _sut.StartExecution(ProjectId, new StartExecutionRequest(1));

        var accepted = result as AcceptedResult;
        Assert.IsNotNull(accepted);
        Assert.AreEqual(202, accepted.StatusCode);
    }

    [TestMethod]
    public async Task GetExecutionStatus_Found_ReturnsOk()
    {
        var status = new AgentExecutionStatus("exec-1", "Running", "Backend", 0.5, null, null, null);
        _orchestrationService
            .Setup(s => s.GetExecutionStatusAsync("exec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var result = await _sut.GetExecutionStatus(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetExecutionStatus_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.GetExecutionStatusAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentExecutionStatus?)null);

        var result = await _sut.GetExecutionStatus(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task CancelExecution_Found_ReturnsOk()
    {
        _orchestrationService
            .Setup(s => s.CancelExecutionAsync("exec-1"))
            .ReturnsAsync(true);

        var result = await _sut.CancelExecution(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CancelExecution_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.CancelExecutionAsync("missing"))
            .ReturnsAsync(false);

        var result = await _sut.CancelExecution(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task PauseExecution_Found_ReturnsOk()
    {
        _orchestrationService
            .Setup(s => s.PauseExecutionAsync("exec-1"))
            .ReturnsAsync(true);

        var result = await _sut.PauseExecution(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task PauseExecution_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.PauseExecutionAsync("missing"))
            .ReturnsAsync(false);

        var result = await _sut.PauseExecution(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
