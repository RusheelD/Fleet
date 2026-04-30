using Fleet.Server.Agents;
using Fleet.Server.Copilot;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentAutoExecutionDispatcherTests
{
    [TestMethod]
    public async Task DispatchAsync_StartsOnlyAllowedLevelsWithinMessageLimit()
    {
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 11, 7, null, "s1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-11");

        var agentService = new Mock<IAgentService>();
        agentService.SetupSequence(service => service.GetExecutionsAsync("p1"))
            .ReturnsAsync(Array.Empty<AgentExecutionDto>())
            .ReturnsAsync([CreateExecution("exec-11", 11)]);

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 11)).ReturnsAsync(CreateWorkItem(11, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 12)).ReturnsAsync(CreateWorkItem(12, levelId: 2));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync(
        [
            new WorkItemLevelDto(1, "Bug", "bug", "#ff0000", 0, true),
            new WorkItemLevelDto(2, "Feature", "feature", "#00ff00", 1, true),
        ]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 1,
            maxActiveExecutionsPerSession: 5);

        var result = await dispatcher.DispatchAsync("p1", "s1", 7, [11, 12]);

        Assert.AreEqual(1, result.StartedExecutionIds.Count);
        Assert.AreEqual("exec-11", result.StartedExecutionIds[0]);
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "started"));
        Assert.AreEqual(1, result.WorkItems.Count(item => item.WorkItemNumber == 12 && item.Status == "skipped"));
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 11, 7, null, "s1", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_SkipsWhenSessionActiveExecutionLimitReached()
    {
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 11, 7, null, "s2", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-11");

        var agentService = new Mock<IAgentService>();
        agentService.SetupSequence(service => service.GetExecutionsAsync("p1"))
            .ReturnsAsync(Array.Empty<AgentExecutionDto>())
            .ReturnsAsync([CreateExecution("exec-11", 11)]);

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 11)).ReturnsAsync(CreateWorkItem(11, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 12)).ReturnsAsync(CreateWorkItem(12, levelId: 1));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([new WorkItemLevelDto(1, "Bug", "bug", "#f00", 0, true)]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 5,
            maxActiveExecutionsPerSession: 1);

        _ = await dispatcher.DispatchAsync("p1", "s2", 7, [11]);
        var secondResult = await dispatcher.DispatchAsync("p1", "s2", 7, [12]);

        Assert.AreEqual(1, secondResult.WorkItems.Count(item => item.Status == "skipped"));
        StringAssert.Contains(secondResult.WorkItems[0].Reason, "session already has");
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 12, 7, null, "s2", null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [DataTestMethod]
    [DataRow("balanced")]
    [DataRow("parallel")]
    public async Task DispatchAsync_DynamicPolicyStartsAllAcceptedCandidatesBeyondLegacyCaps(string executionPolicy)
    {
        var sessionId = $"s-{executionPolicy}";
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        foreach (var workItemNumber in new[] { 21, 22, 23, 24 })
        {
            executionDispatcher
                .Setup(service => service.DispatchWorkItemAsync("p1", workItemNumber, 7, "feature/chat", sessionId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync($"exec-{workItemNumber}");
        }

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 21)).ReturnsAsync(CreateWorkItem(21, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 22)).ReturnsAsync(CreateWorkItem(22, levelId: 2));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 23)).ReturnsAsync(CreateWorkItem(23, levelId: 3));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 24)).ReturnsAsync(CreateWorkItem(24, levelId: 4));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync(
        [
            new WorkItemLevelDto(1, "Feature", "lightbulb", "#00B7C3", 0, true),
            new WorkItemLevelDto(2, "Component", "code", "#498205", 1, true),
            new WorkItemLevelDto(3, "Bug", "bug", "#D13438", 2, true),
            new WorkItemLevelDto(4, "Task", "task-list", "#8A8886", 3, true),
        ]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 3,
            maxActiveExecutionsPerSession: 3);

        var result = await dispatcher.DispatchAsync("p1", sessionId, 7, [21, 22, 23, 24], "feature/chat", executionPolicy);

        Assert.AreEqual(4, result.StartedExecutionIds.Count);
        Assert.AreEqual(4, result.WorkItems.Count(item => item.Status == "started"));
        Assert.AreEqual(0, result.WorkItems.Count(item => item.Status == "skipped"));
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", It.IsAny<int>(), 7, "feature/chat", sessionId, null, It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [TestMethod]
    public async Task DispatchAsync_DynamicPolicyCoversChildCandidatesWhenParentStarts()
    {
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 40, 7, "feature/chat", "s-covered", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-40");

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 40)).ReturnsAsync(CreateWorkItem(40, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 41)).ReturnsAsync(CreateWorkItem(41, levelId: 2, parentWorkItemNumber: 40));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 42)).ReturnsAsync(CreateWorkItem(42, levelId: 2, parentWorkItemNumber: 40));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync(
        [
            new WorkItemLevelDto(1, "Feature", "lightbulb", "#00B7C3", 0, true),
            new WorkItemLevelDto(2, "Task", "task-list", "#8A8886", 1, true),
        ]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 3,
            maxActiveExecutionsPerSession: 3);

        var result = await dispatcher.DispatchAsync("p1", "s-covered", 7, [41, 42, 40], "feature/chat", "balanced");

        Assert.AreEqual(1, result.StartedExecutionIds.Count);
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "started"));
        Assert.AreEqual(2, result.WorkItems.Count(item => item.Status == "covered"));
        Assert.IsTrue(result.WorkItems.Where(item => item.Status == "covered").All(item => item.Reason.Contains("#40", StringComparison.Ordinal)));
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 40, 7, "feature/chat", "s-covered", null, It.IsAny<CancellationToken>()), Times.Once);
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 41, 7, "feature/chat", "s-covered", null, It.IsAny<CancellationToken>()), Times.Never);
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 42, 7, "feature/chat", "s-covered", null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchAsync_DynamicPolicyStartsChildCandidateWhenParentStartFails()
    {
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 50, 7, "feature/chat", "s-parent-fails", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("parent branch could not be prepared"));
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 51, 7, "feature/chat", "s-parent-fails", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-51");

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 50)).ReturnsAsync(CreateWorkItem(50, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 51)).ReturnsAsync(CreateWorkItem(51, levelId: 2, parentWorkItemNumber: 50));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync(
        [
            new WorkItemLevelDto(1, "Feature", "lightbulb", "#00B7C3", 0, true),
            new WorkItemLevelDto(2, "Task", "task-list", "#8A8886", 1, true),
        ]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 3,
            maxActiveExecutionsPerSession: 3);

        var result = await dispatcher.DispatchAsync("p1", "s-parent-fails", 7, [51, 50], "feature/chat", "balanced");

        Assert.AreEqual(1, result.StartedExecutionIds.Count);
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "started"));
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "skipped"));
        Assert.AreEqual(0, result.WorkItems.Count(item => item.Status == "covered"));
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 50, 7, "feature/chat", "s-parent-fails", null, It.IsAny<CancellationToken>()), Times.Once);
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 51, 7, "feature/chat", "s-parent-fails", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_SequentialDynamicPolicyStartsOnlyOneCandidate()
    {
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 31, 7, "feature/chat", "s-sequential", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-31");

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 31)).ReturnsAsync(CreateWorkItem(31, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 32)).ReturnsAsync(CreateWorkItem(32, levelId: 1));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([new WorkItemLevelDto(1, "Task", "task-list", "#8A8886", 0, true)]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 5,
            maxActiveExecutionsPerSession: 5);

        var result = await dispatcher.DispatchAsync("p1", "s-sequential", 7, [31, 32], "feature/chat", "sequential");

        Assert.AreEqual(1, result.StartedExecutionIds.Count);
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "started"));
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "skipped"));
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 31, 7, "feature/chat", "s-sequential", null, It.IsAny<CancellationToken>()), Times.Once);
        executionDispatcher.Verify(service => service.DispatchWorkItemAsync("p1", 32, 7, "feature/chat", "s-sequential", null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchAsync_ReportsSanitizedUnexpectedStartFailure()
    {
        var executionDispatcher = new Mock<IAgentExecutionDispatcher>();
        executionDispatcher
            .Setup(service => service.DispatchWorkItemAsync("p1", 11, 7, "feature/chat", "s3", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Git clone failed for https://token123@github.com/owner/repo.git"));

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 11)).ReturnsAsync(CreateWorkItem(11, levelId: 1));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([new WorkItemLevelDto(1, "Bug", "bug", "#f00", 0, true)]);

        var dispatcher = CreateDispatcher(
            executionDispatcher,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 5,
            maxActiveExecutionsPerSession: 5);

        var result = await dispatcher.DispatchAsync("p1", "s3", 7, [11], "feature/chat");

        Assert.AreEqual(1, result.WorkItems.Count);
        Assert.AreEqual("failed", result.WorkItems[0].Status);
        StringAssert.Contains(result.WorkItems[0].Reason, "Git clone failed");
        StringAssert.Contains(result.WorkItems[0].Reason, "https://***@github.com/owner/repo.git");
        Assert.IsFalse(result.WorkItems[0].Reason.Contains("token123", StringComparison.Ordinal));
    }

    private static AgentAutoExecutionDispatcher CreateDispatcher(
        Mock<IAgentExecutionDispatcher> executionDispatcher,
        Mock<IAgentService> agentService,
        Mock<IWorkItemService> workItemService,
        Mock<IWorkItemLevelService> levelService,
        int maxAutoStartPerMessage,
        int maxActiveExecutionsPerSession)
    {
        var policy = Options.Create(new AgentAutoExecutionDispatchPolicyOptions
        {
            MaxAutoStartPerMessage = maxAutoStartPerMessage,
            MaxActiveExecutionsPerSession = maxActiveExecutionsPerSession,
            AllowedLevels = ["Bug", "Task"],
        });

        return new AgentAutoExecutionDispatcher(
            executionDispatcher.Object,
            agentService.Object,
            workItemService.Object,
            levelService.Object,
            policy,
            new AgentCallCapacityManager(8),
            Mock.Of<ILogger<AgentAutoExecutionDispatcher>>());
    }

    private static WorkItemDto CreateWorkItem(int workItemNumber, int? levelId, int? parentWorkItemNumber = null)
        => new(
            WorkItemNumber: workItemNumber,
            Title: $"Work item {workItemNumber}",
            State: "New",
            Priority: 2,
            Difficulty: 2,
            AssignedTo: "Unassigned",
            Tags: [],
            IsAI: false,
            Description: string.Empty,
            ParentWorkItemNumber: parentWorkItemNumber,
            ChildWorkItemNumbers: [],
            LevelId: levelId);

    private static AgentExecutionDto CreateExecution(string executionId, int workItemNumber)
        => new(
            Id: executionId,
            WorkItemId: workItemNumber,
            WorkItemTitle: $"Work item {workItemNumber}",
            ExecutionMode: "full",
            Status: "running",
            Agents: [],
            StartedAt: "now",
            Duration: string.Empty,
            Progress: 0);
}
