using Fleet.Server.Models;
using Fleet.Server.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class UserServiceTests
{
    private Mock<IUserRepository> _repo = null!;
    private Mock<ILogger<UserService>> _logger = null!;
    private UserService _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _repo = new Mock<IUserRepository>();
        _logger = new Mock<ILogger<UserService>>();
        _sut = new UserService(_repo.Object, _logger.Object);
    }

    // ── GetSettingsAsync ─────────────────────────────────────

    [TestMethod]
    public async Task GetSettingsAsync_ValidUser_ReturnsSettings()
    {
        var profile = new UserProfileDto("John", "john@test.com", "Dev", "NYC", "");
        var connections = new List<LinkedAccountDto>
        {
            new("GitHub", "octocat", "123", DateTime.UtcNow)
        };
        var prefs = new UserPreferencesDto(true, true, false, false, true, false, false);

        _repo.Setup(r => r.GetProfileAsync(UserId)).ReturnsAsync(profile);
        _repo.Setup(r => r.GetConnectionsAsync(UserId)).ReturnsAsync(connections);
        _repo.Setup(r => r.GetPreferencesAsync(UserId)).ReturnsAsync(prefs);

        var result = await _sut.GetSettingsAsync(UserId);

        Assert.AreEqual("John", result.Profile.DisplayName);
        Assert.AreEqual(1, result.Connections.Length);
        Assert.IsTrue(result.Preferences.AgentCompletedNotification);
    }

    [TestMethod]
    public async Task GetSettingsAsync_ProfileNotFound_Throws()
    {
        _repo.Setup(r => r.GetProfileAsync(UserId)).ReturnsAsync((UserProfileDto?)null);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetSettingsAsync(UserId));
    }

    [TestMethod]
    public async Task GetSettingsAsync_PreferencesNotFound_Throws()
    {
        var profile = new UserProfileDto("John", "john@test.com", "Dev", "NYC", "");
        _repo.Setup(r => r.GetProfileAsync(UserId)).ReturnsAsync(profile);
        _repo.Setup(r => r.GetConnectionsAsync(UserId)).ReturnsAsync([]);
        _repo.Setup(r => r.GetPreferencesAsync(UserId)).ReturnsAsync((UserPreferencesDto?)null);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetSettingsAsync(UserId));
    }

    // ── UpdateProfileAsync ───────────────────────────────────

    [TestMethod]
    public async Task UpdateProfileAsync_DelegatesToRepo()
    {
        var request = new UpdateProfileRequest("Jane", "jane@test.com", "Senior Dev", "SF");
        var expected = new UserProfileDto("Jane", "jane@test.com", "Senior Dev", "SF", "");
        _repo.Setup(r => r.UpdateProfileAsync(UserId, request)).ReturnsAsync(expected);

        var result = await _sut.UpdateProfileAsync(UserId, request);

        Assert.AreEqual("Jane", result.DisplayName);
        Assert.AreEqual("SF", result.Location);
    }

    // ── UpdatePreferencesAsync ───────────────────────────────

    [TestMethod]
    public async Task UpdatePreferencesAsync_DelegatesToRepo()
    {
        var prefs = new UserPreferencesDto(false, false, true, true, false, true, true);
        _repo.Setup(r => r.UpdatePreferencesAsync(UserId, prefs)).ReturnsAsync(prefs);

        var result = await _sut.UpdatePreferencesAsync(UserId, prefs);

        Assert.IsFalse(result.AgentCompletedNotification);
        Assert.IsTrue(result.AgentErrorsNotification);
        Assert.IsTrue(result.CompactMode);
    }
}
