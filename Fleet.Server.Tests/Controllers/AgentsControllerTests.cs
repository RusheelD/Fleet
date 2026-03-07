using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class AgentsControllerTests
{
    private Mock<IAgentService> _agentService = null!;
    private Mock<IAgentOrchestrationService> _orchestrationService = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private AgentsController _sut = null!;

    private const string ProjectId = "proj-1";
    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _agentService = new Mock<IAgentService>();
        _orchestrationService = new Mock<IAgentOrchestrationService>();
        _authService = new Mock<IAuthService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new AgentsController(
            _agentService.Object,
            _orchestrationService.Object,
            _authService.Object,
            _eventPublisher.Object);
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
            .Setup(s => s.StartExecutionAsync(ProjectId, 1, UserId, "release/v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-123");

        var result = await _sut.StartExecution(ProjectId, new StartExecutionRequest(1, "release/v1"));

        var accepted = result as AcceptedResult;
        Assert.IsNotNull(accepted);
        Assert.AreEqual(202, accepted.StatusCode);
        _orchestrationService.Verify(
            s => s.StartExecutionAsync(ProjectId, 1, UserId, "release/v1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetExecutionStatus_Found_ReturnsOk()
    {
        var status = new AgentExecutionStatus("exec-1", "Running", "Backend", 0.5, null, null, null);
        _orchestrationService
            .Setup(s => s.GetExecutionStatusAsync(ProjectId, "exec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var result = await _sut.GetExecutionStatus(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetExecutionStatus_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.GetExecutionStatusAsync(ProjectId, "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentExecutionStatus?)null);

        var result = await _sut.GetExecutionStatus(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task CancelExecution_Found_ReturnsOk()
    {
        _orchestrationService
            .Setup(s => s.CancelExecutionAsync(ProjectId, "exec-1"))
            .ReturnsAsync(true);

        var result = await _sut.CancelExecution(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CancelExecution_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.CancelExecutionAsync(ProjectId, "missing"))
            .ReturnsAsync(false);

        var result = await _sut.CancelExecution(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task PauseExecution_Found_ReturnsOk()
    {
        _orchestrationService
            .Setup(s => s.PauseExecutionAsync(ProjectId, "exec-1"))
            .ReturnsAsync(true);

        var result = await _sut.PauseExecution(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task PauseExecution_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.PauseExecutionAsync(ProjectId, "missing"))
            .ReturnsAsync(false);

        var result = await _sut.PauseExecution(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task RetryExecution_WhenStopped_ReturnsAccepted()
    {
        _orchestrationService
            .Setup(s => s.GetExecutionStatusAsync(ProjectId, "exec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionStatus("exec-1", "failed", "Done", 1.0, null, null, null));
        _orchestrationService
            .Setup(s => s.RetryExecutionAsync(ProjectId, "exec-1", UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-2");

        var result = await _sut.RetryExecution(ProjectId, "exec-1");

        Assert.IsInstanceOfType<AcceptedResult>(result);
    }

    [TestMethod]
    public async Task RetryExecution_WhenRunning_ReturnsConflict()
    {
        _orchestrationService
            .Setup(s => s.GetExecutionStatusAsync(ProjectId, "exec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionStatus("exec-1", "running", "Backend", 0.5, null, null, null));

        var result = await _sut.RetryExecution(ProjectId, "exec-1");

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task GetExecutionDocumentation_Found_ReturnsOk()
    {
        var docs = new ExecutionDocumentationDto(
            ExecutionId: "exec-1",
            Title: "fleet-execution-1-exec-1.md",
            Markdown: "# Docs",
            PullRequestUrl: "https://github.com/org/repo/pull/1",
            DiffUrl: "https://github.com/org/repo/pull/1/files");
        _orchestrationService
            .Setup(s => s.GetExecutionDocumentationAsync(ProjectId, "exec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(docs);

        var result = await _sut.GetExecutionDocumentation(ProjectId, "exec-1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ClearLogs_ReturnsOk()
    {
        _agentService.Setup(s => s.ClearLogsAsync(ProjectId))
            .ReturnsAsync(5);

        var result = await _sut.ClearLogs(ProjectId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetExecutionDocumentation_NotFound_Returns404()
    {
        _orchestrationService
            .Setup(s => s.GetExecutionDocumentationAsync(ProjectId, "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExecutionDocumentationDto?)null);

        var result = await _sut.GetExecutionDocumentation(ProjectId, "missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
