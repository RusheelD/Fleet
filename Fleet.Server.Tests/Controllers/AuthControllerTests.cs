using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class AuthControllerTests
{
    private Mock<IAuthService> _authService = null!;
    private AuthController _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authService = new Mock<IAuthService>();
        _sut = new AuthController(_authService.Object);
    }

    [TestMethod]
    public async Task GetCurrentUser_ReturnsOk()
    {
        var profile = new UserProfileDto("Jane", "jane@test.com", "", "", "");
        _authService.Setup(a => a.GetOrCreateCurrentUserAsync()).ReturnsAsync(profile);

        var result = await _sut.GetCurrentUser();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(profile, ok.Value);
    }
}
