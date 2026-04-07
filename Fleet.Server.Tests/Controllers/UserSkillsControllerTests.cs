using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Skills;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class UserSkillsControllerTests
{
    private Mock<ISkillService> _skillService = null!;
    private Mock<IAuthService> _authService = null!;
    private UserSkillsController _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _skillService = new Mock<ISkillService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(service => service.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new UserSkillsController(_skillService.Object, _authService.Object);
    }

    [TestMethod]
    public async Task GetSkills_ReturnsOk()
    {
        var skills = new List<PromptSkillDto>
        {
            new(1, "Bug triage", "desc", "when", "content", true, "personal", null, DateTime.UtcNow, DateTime.UtcNow),
        };
        _skillService.Setup(service => service.GetUserSkillsAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(skills);

        var result = await _sut.GetSkills(CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(skills, ok.Value);
    }
}
