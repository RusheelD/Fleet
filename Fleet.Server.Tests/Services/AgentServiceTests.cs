using Fleet.Server.Agents;
using Fleet.Server.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentServiceTests
{
    private Mock<IAgentTaskRepository> _repo = null!;
    private Mock<ILogger<AgentService>> _logger = null!;
    private AgentService _sut = null!;

    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _repo = new Mock<IAgentTaskRepository>();
        _logger = new Mock<ILogger<AgentService>>();
        _sut = new AgentService(_repo.Object, _logger.Object);
    }

    [TestMethod]
    public async Task GetExecutionsAsync_DelegatesToRepo()
    {
        var executions = new List<AgentExecutionDto>
        {
            new("exec-1", 1, "Build Auth", "Running",
                [new AgentInfoDto("Planner", "Complete", "Done", 1.0)],
                "2024-01-01", "5 min", 0.5),
        };
        _repo.Setup(r => r.GetExecutionsByProjectIdAsync(ProjectId)).ReturnsAsync(executions);

        var result = await _sut.GetExecutionsAsync(ProjectId);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("exec-1", result[0].Id);
        Assert.AreEqual("Build Auth", result[0].WorkItemTitle);
    }

    [TestMethod]
    public async Task GetExecutionsAsync_Empty_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetExecutionsByProjectIdAsync(ProjectId)).ReturnsAsync([]);

        var result = await _sut.GetExecutionsAsync(ProjectId);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetLogsAsync_DelegatesToRepo()
    {
        var logs = new List<LogEntryDto>
        {
            new("12:00", "Planner", "Info", "Starting planning phase"),
            new("12:01", "Backend", "Info", "Generating code"),
        };
        _repo.Setup(r => r.GetLogsByProjectIdAsync(ProjectId)).ReturnsAsync(logs);

        var result = await _sut.GetLogsAsync(ProjectId);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Planner", result[0].Agent);
    }

    [TestMethod]
    public async Task GetLogsAsync_Empty_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetLogsByProjectIdAsync(ProjectId)).ReturnsAsync([]);

        var result = await _sut.GetLogsAsync(ProjectId);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ClearLogsAsync_DelegatesToRepo()
    {
        _repo.Setup(r => r.ClearLogsByProjectIdAsync(ProjectId)).ReturnsAsync(4);

        var result = await _sut.ClearLogsAsync(ProjectId);

        Assert.AreEqual(4, result);
    }

    [TestMethod]
    public async Task ClearExecutionLogsAsync_DelegatesToRepo()
    {
        _repo.Setup(r => r.ClearLogsByExecutionIdAsync(ProjectId, "exec-1")).ReturnsAsync(2);

        var result = await _sut.ClearExecutionLogsAsync(ProjectId, "exec-1");

        Assert.AreEqual(2, result);
    }
}
