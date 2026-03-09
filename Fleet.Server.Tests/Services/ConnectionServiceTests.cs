using Fleet.Server.Connections;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ConnectionServiceTests
{
    private Mock<IConnectionRepository> _connectionRepo = null!;
    private Mock<IHttpClientFactory> _httpClientFactory = null!;
    private Mock<IConfiguration> _configuration = null!;
    private Mock<ILogger<ConnectionService>> _logger = null!;
    private ConnectionService _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _connectionRepo = new Mock<IConnectionRepository>();
        _httpClientFactory = new Mock<IHttpClientFactory>();
        _configuration = new Mock<IConfiguration>();
        _logger = new Mock<ILogger<ConnectionService>>();

        _configuration.Setup(c => c["GitHub:ClientId"]).Returns("test-client-id");
        _configuration.Setup(c => c["GitHub:ClientSecret"]).Returns("test-client-secret");

        _sut = new ConnectionService(
            _connectionRepo.Object,
            _httpClientFactory.Object,
            _configuration.Object,
            _logger.Object);
    }

    // ── GetConnectionsAsync ──────────────────────────────────

    [TestMethod]
    public async Task GetConnectionsAsync_DelegatesToRepo()
    {
        var connections = new List<LinkedAccountDto>
        {
            new(1, "GitHub", "octocat", "12345", DateTime.UtcNow)
        };
        _connectionRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(connections);

        var result = await _sut.GetConnectionsAsync(UserId);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("GitHub", result[0].Provider);
        Assert.AreEqual("octocat", result[0].ConnectedAs);
    }

    [TestMethod]
    public async Task GetConnectionsAsync_Empty_ReturnsEmpty()
    {
        _connectionRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync([]);

        var result = await _sut.GetConnectionsAsync(UserId);

        Assert.AreEqual(0, result.Count);
    }

    // ── UnlinkGitHubAsync ────────────────────────────────────

    [TestMethod]
    public async Task UnlinkGitHubAsync_ExistingAccount_Deletes()
    {
        var account = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            UserProfileId = UserId
        };
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub")).ReturnsAsync(account);

        await _sut.UnlinkGitHubAsync(UserId);

        _connectionRepo.Verify(r => r.DeleteAsync(account), Times.Once);
    }

    [TestMethod]
    public async Task UnlinkGitHubAsync_NoAccount_Throws()
    {
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync((LinkedAccount?)null);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.UnlinkGitHubAsync(UserId));
    }

    // ── LinkGitHubAsync ──────────────────────────────────────

    [TestMethod]
    public async Task LinkGitHubAsync_MissingClientId_Throws()
    {
        _configuration.Setup(c => c["GitHub:ClientId"]).Returns((string?)null);
        var sut = new ConnectionService(
            _connectionRepo.Object, _httpClientFactory.Object,
            _configuration.Object, _logger.Object);
        var state = await sut.CreateGitHubOAuthStateAsync(UserId);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => sut.LinkGitHubAsync(UserId, "code", "http://redirect", state.State));
    }

    [TestMethod]
    public async Task LinkGitHubAsync_MissingState_ThrowsUnauthorized()
    {
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            () => _sut.LinkGitHubAsync(UserId, "code", "http://redirect", ""));
    }

    [TestMethod]
    public async Task LinkGitHubAsync_MissingClientSecret_Throws()
    {
        _configuration.Setup(c => c["GitHub:ClientSecret"]).Returns((string?)null);
        var sut = new ConnectionService(
            _connectionRepo.Object, _httpClientFactory.Object,
            _configuration.Object, _logger.Object);
        var state = await sut.CreateGitHubOAuthStateAsync(UserId);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => sut.LinkGitHubAsync(UserId, "code", "http://redirect", state.State));
    }

    // ── GetGitHubRepositoriesAsync ───────────────────────────

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_NoAccount_Throws()
    {
        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([]);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetGitHubRepositoriesAsync(UserId));
    }

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_NoToken_Throws()
    {
        var account = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = null,
            UserProfileId = UserId
        };
        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([account]);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetGitHubRepositoriesAsync(UserId));
    }
}
