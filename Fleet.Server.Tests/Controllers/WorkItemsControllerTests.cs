using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Realtime;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class WorkItemsControllerTests
{
    private Mock<IWorkItemService> _service = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private WorkItemsController _sut = null!;

    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _service = new Mock<IWorkItemService>();
        _authService = new Mock<IAuthService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        _sut = new WorkItemsController(_service.Object, _authService.Object, _eventPublisher.Object);
    }

    [TestMethod]
    public async Task GetByProject_ReturnsOk()
    {
        _service.Setup(s => s.GetByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<WorkItemDto>());

        var result = await _sut.GetByProject(ProjectId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetByWorkItemNumber_Found_ReturnsOk()
    {
        var item = new WorkItemDto(1, "Task", "Active", 1, 3, "user", [], false, "", null, [], null);
        _service.Setup(s => s.GetByWorkItemNumberAsync(ProjectId, 1)).ReturnsAsync(item);

        var result = await _sut.GetByWorkItemNumber(ProjectId, 1);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetByWorkItemNumber_NotFound_Returns404()
    {
        _service.Setup(s => s.GetByWorkItemNumberAsync(ProjectId, 99))
            .ReturnsAsync((WorkItemDto?)null);

        var result = await _sut.GetByWorkItemNumber(ProjectId, 99);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var request = new CreateWorkItemRequest("New", "Desc", 1, 3, "New", "user", [], false, null, null);
        var item = new WorkItemDto(1, "New", "New", 1, 3, "user", [], false, "Desc", null, [], null);
        _service.Setup(s => s.CreateAsync(ProjectId, request)).ReturnsAsync(item);

        var result = await _sut.Create(ProjectId, request);

        var created = result as CreatedAtActionResult;
        Assert.IsNotNull(created);
        Assert.AreEqual(201, created.StatusCode);
    }

    [TestMethod]
    public async Task Update_Found_ReturnsOk()
    {
        var request = new UpdateWorkItemRequest("Updated", null, null, null, null, null, null, null, null, null);
        var item = new WorkItemDto(1, "Updated", "Active", 1, 3, "user", [], false, "", null, [], null);
        _service.Setup(s => s.UpdateAsync(ProjectId, 1, request)).ReturnsAsync(item);

        var result = await _sut.Update(ProjectId, 1, request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateWorkItemRequest("Updated", null, null, null, null, null, null, null, null, null);
        _service.Setup(s => s.UpdateAsync(ProjectId, 99, request)).ReturnsAsync((WorkItemDto?)null);

        var result = await _sut.Update(ProjectId, 99, request);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task Delete_Found_ReturnsNoContent()
    {
        _service.Setup(s => s.DeleteAsync(ProjectId, 1)).ReturnsAsync(true);

        var result = await _sut.Delete(ProjectId, 1);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task Delete_NotFound_Returns404()
    {
        _service.Setup(s => s.DeleteAsync(ProjectId, 99)).ReturnsAsync(false);

        var result = await _sut.Delete(ProjectId, 99);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
