using Fleet.Server.Copilot.Tools;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class CreateProjectToolTests
{
    [TestMethod]
    public async Task ExecuteAsync_ProjectScope_ReturnsScopeError()
    {
        var projectService = new Mock<IProjectService>();
        var sut = new CreateProjectTool(projectService.Object);

        var result = await sut.ExecuteAsync(
            """{"title":"Test","repo":"owner/repo"}""",
            new ChatToolContext("proj-1", "42"));

        Assert.IsTrue(result.Contains("only available in global chat", StringComparison.OrdinalIgnoreCase));
        projectService.Verify(service => service.CreateProjectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_GlobalScope_CreatesProject()
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.CreateProjectAsync(
                "My Project",
                "Desc",
                "owner/repo",
                null,
                null,
                null,
                null))
            .ReturnsAsync(new ProjectDto(
                Id: "proj-123",
                OwnerId: "42",
                Title: "My Project",
                Slug: "my-project",
                Description: "Desc",
                Repo: "owner/repo",
                WorkItems: new WorkItemSummaryDto(0, 0, 0),
                Agents: new AgentSummaryDto(0, 0),
                LastActivity: "just now"));

        var sut = new CreateProjectTool(projectService.Object);

        var result = await sut.ExecuteAsync(
            """{"title":"My Project","description":"Desc","repo":"owner/repo"}""",
            new ChatToolContext(null, "42"));

        Assert.IsTrue(result.Contains("Project created successfully", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.Contains("proj-123", StringComparison.Ordinal));
    }
}
