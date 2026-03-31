using Fleet.Server.Agents;
using Fleet.Server.Agents.Tools;
using Fleet.Server.Connections;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class CommitAndPushToolTests
{
    [TestMethod]
    public async Task ExecuteAsync_ResolvesFreshTokenBeforePushing()
    {
        var connectionService = new Mock<IConnectionService>();
        var sandbox = new Mock<IRepoSandbox>();
        var tool = new CommitAndPushTool(connectionService.Object);
        var context = new AgentToolContext(
            sandbox.Object,
            "project-1",
            "42",
            "stale-token",
            "octocat/hello-world",
            "exec-1");

        connectionService
            .Setup(service => service.ResolveGitHubAccessTokenForRepoAsync(
                42,
                "octocat/hello-world",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("fresh-token");

        var result = await tool.ExecuteAsync(
            """{"commit_message":"Ship changes"}""",
            context,
            CancellationToken.None);

        sandbox.Verify(repo => repo.CommitAndPushAsync(
            "fresh-token",
            "Ship changes",
            "Fleet Agent",
            "agent@fleet.dev",
            It.IsAny<CancellationToken>()),
            Times.Once);
        StringAssert.Contains(result, "Ship changes");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoAccessibleGitHubAccount_ReturnsError()
    {
        var connectionService = new Mock<IConnectionService>();
        var sandbox = new Mock<IRepoSandbox>();
        var tool = new CommitAndPushTool(connectionService.Object);
        var context = new AgentToolContext(
            sandbox.Object,
            "project-1",
            "42",
            "stale-token",
            "octocat/hello-world",
            "exec-1");

        connectionService
            .Setup(service => service.ResolveGitHubAccessTokenForRepoAsync(
                42,
                "octocat/hello-world",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await tool.ExecuteAsync(
            """{"commit_message":"Ship changes"}""",
            context,
            CancellationToken.None);

        sandbox.Verify(repo => repo.CommitAndPushAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        StringAssert.Contains(result, "no linked GitHub account can access");
    }
}
