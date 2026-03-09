using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class ConnectionsControllerTests
{
    private Mock<IConnectionService> _connectionService = null!;
    private Mock<IAuthService> _authService = null!;
    private IConfiguration _configuration = null!;
    private ConnectionsController _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _connectionService = new Mock<IConnectionService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:ClientId"] = "test-client-id"
            })
            .Build();
        _sut = new ConnectionsController(_connectionService.Object, _authService.Object, _configuration);
    }

    [TestMethod]
    public async Task GetConnections_ReturnsOk()
    {
        var connections = new List<LinkedAccountDto>
        {
            new(1, "GitHub", "user123", "ext-1", DateTime.UtcNow)
        };
        _connectionService.Setup(s => s.GetConnectionsAsync(UserId)).ReturnsAsync(connections);

        var result = await _sut.GetConnections();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(connections, ok.Value);
    }

    [TestMethod]
    public async Task LinkGitHub_ReturnsOk()
    {
        var dto = new LinkedAccountDto(1, "GitHub", "user123", "ext-1", DateTime.UtcNow);
        _connectionService.Setup(s => s.LinkGitHubAsync(UserId, "code123", "http://redirect", "state-123"))
            .ReturnsAsync(dto);

        var result = await _sut.LinkGitHub(new LinkGitHubRequest("code123", "http://redirect", "state-123"));

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(dto, ok.Value);
    }

    [TestMethod]
    public async Task UnlinkGitHub_ReturnsNoContent()
    {
        _connectionService.Setup(s => s.UnlinkGitHubAsync(UserId)).Returns(Task.CompletedTask);

        var result = await _sut.UnlinkGitHub();

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task UnlinkGitHubById_ReturnsNoContent()
    {
        _connectionService.Setup(s => s.UnlinkGitHubAsync(UserId, 7)).Returns(Task.CompletedTask);

        var result = await _sut.UnlinkGitHubById(7);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task GetGitHubRepos_ReturnsOk()
    {
        var repos = new List<GitHubRepoDto>();
        _connectionService.Setup(s => s.GetGitHubRepositoriesAsync(UserId, null)).ReturnsAsync(repos);

        var result = await _sut.GetGitHubRepos();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(repos, ok.Value);
    }

    [TestMethod]
    public async Task GetGitHubRepos_WithAccountId_ReturnsOk()
    {
        var repos = new List<GitHubRepoDto>();
        _connectionService.Setup(s => s.GetGitHubRepositoriesAsync(UserId, 9)).ReturnsAsync(repos);

        var result = await _sut.GetGitHubRepos(9);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(repos, ok.Value);
    }
}
