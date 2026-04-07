using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class ProjectSkillsControllerTests
{
    private Mock<ISkillService> _skillService = null!;
    private Mock<IAuthService> _authService = null!;
    private ProjectSkillsController _sut = null!;

    private const int UserId = 42;
    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _skillService = new Mock<ISkillService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(service => service.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new ProjectSkillsController(_skillService.Object, _authService.Object);
    }

    [TestMethod]
    public async Task GetSkills_ReturnsOk()
    {
        var skills = new List<PromptSkillDto>
        {
            new(1, "Release gate", "desc", "when", "content", true, "project", ProjectId, DateTime.UtcNow, DateTime.UtcNow),
        };
        _skillService.Setup(service => service.GetProjectSkillsAsync(UserId, ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(skills);

        var result = await _sut.GetSkills(ProjectId, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(skills, ok.Value);
    }
}
