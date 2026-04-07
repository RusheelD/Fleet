using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Memories;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class ProjectMemoriesControllerTests
{
    private Mock<IMemoryService> _memoryService = null!;
    private Mock<IAuthService> _authService = null!;
    private ProjectMemoriesController _sut = null!;

    private const int UserId = 42;
    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _memoryService = new Mock<IMemoryService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(service => service.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new ProjectMemoriesController(_memoryService.Object, _authService.Object);
    }

    [TestMethod]
    public async Task GetMemories_ReturnsOk()
    {
        var memories = new List<MemoryEntryDto>
        {
            new(9, "Release deadline", "GA date is fixed", "project", "Launch on 2026-05-15.", false, "project", ProjectId, DateTime.UtcNow, DateTime.UtcNow, false, null),
        };
        _memoryService.Setup(service => service.GetProjectMemoriesAsync(UserId, ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(memories);

        var result = await _sut.GetMemories(ProjectId, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(memories, ok.Value);
    }
}
