using Fleet.Server.Agents;
using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentExecutionDispatcherTests
{
    [TestMethod]
    public async Task DispatchWorkItemAsync_DoesNotUseProjectBranchPatternAsTargetBranch()
    {
        await using var db = CreateDbContext();
        db.Projects.Add(new Project
        {
            Id = "p1",
            Title = "Project",
            Slug = "project",
            Repo = "owner/repo",
            BranchPattern = "fleet/{workItemNumber}-{slug}",
        });
        db.ChatSessions.Add(new ChatSession
        {
            Id = "s1",
            ProjectId = "p1",
            OwnerId = "7",
            Title = "Dynamic iteration",
            LastMessage = string.Empty,
            Timestamp = DateTime.UtcNow.ToString("O"),
            IsActive = true,
            BranchStrategy = ChatSessionBranchStrategy.AutoFromProjectPattern,
        });
        await db.SaveChangesAsync();

        var orchestrationService = new Mock<IAgentOrchestrationService>();
        orchestrationService
            .Setup(service => service.StartExecutionAsync("p1", 160, 7, (string?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-160");

        var chatSessionRepository = new Mock<IChatSessionRepository>();
        chatSessionRepository
            .Setup(repository => repository.AppendSessionActivityAsync(
                "p1",
                "s1",
                It.IsAny<ChatSessionActivityDto>(),
                null))
            .Returns(Task.CompletedTask);

        var sut = new AgentExecutionDispatcher(db, orchestrationService.Object, chatSessionRepository.Object);

        var executionId = await sut.DispatchWorkItemAsync("p1", 160, 7, chatSessionId: "s1");

        Assert.AreEqual("exec-160", executionId);
        orchestrationService.Verify(
            service => service.StartExecutionAsync("p1", 160, 7, (string?)null, It.IsAny<CancellationToken>()),
            Times.Once);
        orchestrationService.Verify(
            service => service.StartExecutionAsync("p1", 160, 7, "fleet/{workItemNumber}-{slug}", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task DispatchWorkItemAsync_UsesRequestedTargetBranchWhenProvided()
    {
        await using var db = CreateDbContext();

        var orchestrationService = new Mock<IAgentOrchestrationService>();
        orchestrationService
            .Setup(service => service.StartExecutionAsync("p1", 161, 7, "release/v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exec-161");

        var sut = new AgentExecutionDispatcher(
            db,
            orchestrationService.Object,
            Mock.Of<IChatSessionRepository>());

        var executionId = await sut.DispatchWorkItemAsync("p1", 161, 7, requestedTargetBranch: " release/v1 ");

        Assert.AreEqual("exec-161", executionId);
        orchestrationService.Verify(
            service => service.StartExecutionAsync("p1", 161, 7, "release/v1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static FleetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new FleetDbContext(options);
    }
}
