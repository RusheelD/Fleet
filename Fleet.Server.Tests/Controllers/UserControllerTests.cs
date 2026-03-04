using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Users;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class UserControllerTests
{
    private Mock<IUserService> _userService = null!;
    private Mock<IAuthService> _authService = null!;
    private UserController _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _userService = new Mock<IUserService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new UserController(_userService.Object, _authService.Object);
    }

    // ── GetSettings ──────────────────────────────────────

    [TestMethod]
    public async Task GetSettings_ReturnsOk()
    {
        var settings = new UserSettingsDto(
            new UserProfileDto("Name", "e@e.com", "", "", ""),
            [],
            new UserPreferencesDto(true, true, true, true, false, false, false));
        _userService.Setup(s => s.GetSettingsAsync(UserId)).ReturnsAsync(settings);

        var result = await _sut.GetSettings();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(settings, ok.Value);
    }

    // ── UpdateProfile ────────────────────────────────────

    [TestMethod]
    public async Task UpdateProfile_ReturnsOk()
    {
        var profile = new UserProfileDto("New Name", "e@e.com", "bio", "loc", "");
        var request = new UpdateProfileRequest("New Name", "e@e.com", "bio", "loc");
        _userService.Setup(s => s.UpdateProfileAsync(UserId, request)).ReturnsAsync(profile);

        var result = await _sut.UpdateProfile(request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(profile, ok.Value);
    }

    // ── UpdatePreferences ────────────────────────────────

    [TestMethod]
    public async Task UpdatePreferences_ReturnsOk()
    {
        var prefs = new UserPreferencesDto(true, true, true, true, true, false, false);
        _userService.Setup(s => s.UpdatePreferencesAsync(UserId, prefs)).ReturnsAsync(prefs);

        var result = await _sut.UpdatePreferences(prefs);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(prefs, ok.Value);
    }
}
