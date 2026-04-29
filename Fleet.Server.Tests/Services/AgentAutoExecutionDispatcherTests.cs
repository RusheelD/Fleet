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
        var orchestrationService = new Mock<IAgentOrchestrationService>();
        orchestrationService
            .Setup(service => service.StartExecutionAsync("p1", 11, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-11");

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

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
            orchestrationService,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 1,
            maxActiveExecutionsPerSession: 5);

        var result = await dispatcher.DispatchAsync("p1", "s1", 7, [11, 12]);

        Assert.AreEqual(1, result.StartedExecutionIds.Count);
        Assert.AreEqual("exec-11", result.StartedExecutionIds[0]);
        Assert.AreEqual(1, result.WorkItems.Count(item => item.Status == "started"));
        Assert.AreEqual(0, result.WorkItems.Count(item => item.WorkItemNumber == 12));
        orchestrationService.Verify(service => service.StartExecutionAsync("p1", 11, 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DispatchAsync_SkipsWhenSessionActiveExecutionLimitReached()
    {
        var orchestrationService = new Mock<IAgentOrchestrationService>();
        orchestrationService
            .Setup(service => service.StartExecutionAsync("p1", 11, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-11");

        var agentService = new Mock<IAgentService>();
        agentService.Setup(service => service.GetExecutionsAsync("p1")).ReturnsAsync(Array.Empty<AgentExecutionDto>());

        var workItemService = new Mock<IWorkItemService>();
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 11)).ReturnsAsync(CreateWorkItem(11, levelId: 1));
        workItemService.Setup(service => service.GetByWorkItemNumberAsync("p1", 12)).ReturnsAsync(CreateWorkItem(12, levelId: 1));

        var levelService = new Mock<IWorkItemLevelService>();
        levelService.Setup(service => service.GetByProjectIdAsync("p1")).ReturnsAsync([new WorkItemLevelDto(1, "Bug", "bug", "#f00", 0, true)]);

        var dispatcher = CreateDispatcher(
            orchestrationService,
            agentService,
            workItemService,
            levelService,
            maxAutoStartPerMessage: 5,
            maxActiveExecutionsPerSession: 1);

        _ = await dispatcher.DispatchAsync("p1", "s2", 7, [11]);
        var secondResult = await dispatcher.DispatchAsync("p1", "s2", 7, [12]);

        Assert.AreEqual(1, secondResult.WorkItems.Count(item => item.Status == "skipped"));
        StringAssert.Contains(secondResult.WorkItems[0].Reason, "session already has");
        orchestrationService.Verify(service => service.StartExecutionAsync("p1", 12, 7, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AgentAutoExecutionDispatcher CreateDispatcher(
        Mock<IAgentOrchestrationService> orchestrationService,
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
            orchestrationService.Object,
            agentService.Object,
            workItemService.Object,
            levelService.Object,
            policy,
            new AgentCallCapacityManager(8),
            Mock.Of<ILogger<AgentAutoExecutionDispatcher>>());
    }

    private static WorkItemDto CreateWorkItem(int workItemNumber, int? levelId)
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
            ParentWorkItemNumber: null,
            ChildWorkItemNumbers: [],
            LevelId: levelId);
}
