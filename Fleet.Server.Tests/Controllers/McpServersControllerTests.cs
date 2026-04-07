using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Mcp;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class McpServersControllerTests
{
    private Mock<IMcpServerService> _serverService = null!;
    private Mock<IAuthService> _authService = null!;
    private McpServersController _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _serverService = new Mock<IMcpServerService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(service => service.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new McpServersController(_serverService.Object, _authService.Object);
    }

    [TestMethod]
    public async Task GetServers_ReturnsOk()
    {
        var servers = new List<McpServerDto>
        {
            new(
                1,
                "Playwright",
                "Browser automation",
                "stdio",
                "npx",
                ["-y", "@playwright/mcp@latest"],
                null,
                null,
                "playwright",
                true,
                [],
                [],
                DateTime.UtcNow,
                DateTime.UtcNow,
                null,
                null,
                0,
                [])
        };
        _serverService.Setup(service => service.GetServersAsync(UserId)).ReturnsAsync(servers);

        var result = await _sut.GetServers();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(servers, ok.Value);
    }

    [TestMethod]
    public async Task GetTemplates_ReturnsOk()
    {
        var templates = new List<McpServerTemplateDto>
        {
            new("playwright", "Playwright", "desc", "stdio", "npx", ["-y"], null, null, [], [], [])
        };
        _serverService.Setup(service => service.GetBuiltInTemplatesAsync()).ReturnsAsync(templates);

        var result = await _sut.GetTemplates();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(templates, ok.Value);
    }

    [TestMethod]
    public async Task CreateServer_ReturnsOk()
    {
        var request = new UpsertMcpServerRequest("Playwright", "desc", "stdio", "npx", ["-y"], null, null, "playwright", true, [], []);
        var created = new McpServerDto(7, "Playwright", "desc", "stdio", "npx", ["-y"], null, null, "playwright", true, [], [], DateTime.UtcNow, DateTime.UtcNow, null, null, 0, []);
        _serverService.Setup(service => service.CreateAsync(UserId, request)).ReturnsAsync(created);

        var result = await _sut.CreateServer(request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(created, ok.Value);
    }

    [TestMethod]
    public async Task UpdateServer_ReturnsOk()
    {
        var request = new UpsertMcpServerRequest("Playwright", "desc", "stdio", "npx", ["-y"], null, null, "playwright", true, [], []);
        var updated = new McpServerDto(7, "Playwright", "desc", "stdio", "npx", ["-y"], null, null, "playwright", true, [], [], DateTime.UtcNow, DateTime.UtcNow, null, null, 0, []);
        _serverService.Setup(service => service.UpdateAsync(UserId, 7, request)).ReturnsAsync(updated);

        var result = await _sut.UpdateServer(7, request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(updated, ok.Value);
    }

    [TestMethod]
    public async Task DeleteServer_ReturnsNoContent()
    {
        _serverService.Setup(service => service.DeleteAsync(UserId, 7)).Returns(Task.CompletedTask);

        var result = await _sut.DeleteServer(7);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task ValidateServer_ReturnsOk()
    {
        var validation = new McpServerValidationResultDto(true, null, 3, ["browser_navigate", "browser_click", "browser_snapshot"]);
        _serverService.Setup(service => service.ValidateAsync(UserId, 9, It.IsAny<CancellationToken>())).ReturnsAsync(validation);

        var result = await _sut.ValidateServer(9, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(validation, ok.Value);
    }
}
