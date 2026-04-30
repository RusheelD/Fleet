using Fleet.Server.Copilot;
using Fleet.Server.GitHub;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class DynamicIterationDispatchServiceTests
{
    [TestMethod]
    public async Task DispatchFromToolEventsAsync_SkipsMainWhenRepositoryHasBranchProtection()
    {
        var autoDispatcher = new Mock<IAgentAutoExecutionDispatcher>();
        var projectRepository = CreateProjectRepository();
        var gitHubApi = new Mock<IGitHubApiService>();
        gitHubApi
            .Setup(service => service.GetBranchesAsync(7, "owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GitHubBranchInfo("main", true, false),
                new GitHubBranchInfo("release", false, true),
            ]);
        var sut = CreateService(autoDispatcher, projectRepository, gitHubApi);

        var result = await sut.DispatchFromToolEventsAsync(
            "p1",
            "s1",
            7,
            [CreateWorkItemMutationEvent(160)],
            "main",
            "balanced");

        Assert.AreEqual(1, result.CandidateCount);
        Assert.AreEqual(0, result.StartedCount);
        Assert.AreEqual(1, result.SkippedCount);
        StringAssert.Contains(result.Notes[0], "main only when the repository has no branch protection");
        autoDispatcher.Verify(dispatcher => dispatcher.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DispatchFromToolEventsAsync_AllowsMainWhenRepositoryHasNoBranchProtection()
    {
        var autoDispatcher = new Mock<IAgentAutoExecutionDispatcher>();
        autoDispatcher
            .Setup(dispatcher => dispatcher.DispatchAsync(
                "p1",
                "s1",
                7,
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 160 })),
                "main",
                "balanced",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentAutoExecutionDispatchResult(
                ["exec-160"],
                [new AgentAutoExecutionWorkItemResult(160, "started", "Started automatically.", "exec-160")]));
        var projectRepository = CreateProjectRepository();
        var gitHubApi = new Mock<IGitHubApiService>();
        gitHubApi
            .Setup(service => service.GetBranchesAsync(7, "owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GitHubBranchInfo("main", true, false),
            ]);
        var sut = CreateService(autoDispatcher, projectRepository, gitHubApi);

        var result = await sut.DispatchFromToolEventsAsync(
            "p1",
            "s1",
            7,
            [CreateWorkItemMutationEvent(160)],
            "main",
            "balanced");

        Assert.AreEqual(1, result.StartedCount);
        Assert.AreEqual(0, result.SkippedCount);
        autoDispatcher.VerifyAll();
    }

    [TestMethod]
    public async Task DispatchFromToolEventsAsync_SkipsDefaultMainWhenNoTargetProvidedAndMainProtected()
    {
        var autoDispatcher = new Mock<IAgentAutoExecutionDispatcher>();
        var projectRepository = CreateProjectRepository();
        var gitHubApi = new Mock<IGitHubApiService>();
        gitHubApi
            .Setup(service => service.GetBranchesAsync(7, "owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GitHubBranchInfo("main", true, true),
            ]);
        var sut = CreateService(autoDispatcher, projectRepository, gitHubApi);

        var result = await sut.DispatchFromToolEventsAsync(
            "p1",
            "s1",
            7,
            [CreateWorkItemMutationEvent(160)],
            null,
            "balanced");

        Assert.AreEqual(1, result.SkippedCount);
        StringAssert.Contains(result.Notes[0], "main only when the repository has no branch protection");
        autoDispatcher.Verify(dispatcher => dispatcher.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DynamicIterationDispatchService CreateService(
        Mock<IAgentAutoExecutionDispatcher> autoDispatcher,
        Mock<IProjectRepository> projectRepository,
        Mock<IGitHubApiService> gitHubApi)
        => new(
            autoDispatcher.Object,
            projectRepository.Object,
            gitHubApi.Object,
            Mock.Of<ILogger<DynamicIterationDispatchService>>());

    private static Mock<IProjectRepository> CreateProjectRepository()
    {
        var repository = new Mock<IProjectRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync("p1", "7"))
            .ReturnsAsync(new ProjectDto(
                "p1",
                "7",
                "Project",
                "project",
                "Desc",
                "owner/repo",
                new WorkItemSummaryDto(0, 0, 0),
                new AgentSummaryDto(0, 0),
                "just now"));
        return repository;
    }

    private static ToolEventDto CreateWorkItemMutationEvent(int workItemNumber)
        => new(
            "bulk_create_work_items",
            "{}",
            $$"""[{"workItemNumber":{{workItemNumber}}}]""");
}
