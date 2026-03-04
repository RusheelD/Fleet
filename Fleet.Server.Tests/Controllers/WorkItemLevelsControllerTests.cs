using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class WorkItemLevelsControllerTests
{
    private Mock<IWorkItemLevelService> _service = null!;
    private WorkItemLevelsController _sut = null!;

    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _service = new Mock<IWorkItemLevelService>();
        _sut = new WorkItemLevelsController(_service.Object);
    }

    [TestMethod]
    public async Task GetByProject_EnsuresDefaultsThenReturnsOk()
    {
        _service.Setup(s => s.EnsureDefaultLevelsAsync(ProjectId)).Returns(Task.CompletedTask);
        _service.Setup(s => s.GetByProjectIdAsync(ProjectId))
            .ReturnsAsync(new List<WorkItemLevelDto>());

        var result = await _sut.GetByProject(ProjectId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _service.Verify(s => s.EnsureDefaultLevelsAsync(ProjectId), Times.Once);
    }

    [TestMethod]
    public async Task GetById_Found_ReturnsOk()
    {
        var level = new WorkItemLevelDto(1, "Epic", "Crown20", "#7B68EE", 0, true);
        _service.Setup(s => s.GetByIdAsync(ProjectId, 1)).ReturnsAsync(level);

        var result = await _sut.GetById(ProjectId, 1);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetById_NotFound_Returns404()
    {
        _service.Setup(s => s.GetByIdAsync(ProjectId, 99))
            .ReturnsAsync((WorkItemLevelDto?)null);

        var result = await _sut.GetById(ProjectId, 99);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var request = new CreateWorkItemLevelRequest("Sprint", "Timer", "#FF9800", 3);
        var level = new WorkItemLevelDto(3, "Sprint", "Timer", "#FF9800", 3, false);
        _service.Setup(s => s.CreateAsync(ProjectId, request)).ReturnsAsync(level);

        var result = await _sut.Create(ProjectId, request);

        var created = result as CreatedAtActionResult;
        Assert.IsNotNull(created);
        Assert.AreEqual(201, created.StatusCode);
    }

    [TestMethod]
    public async Task Update_Found_ReturnsOk()
    {
        var request = new UpdateWorkItemLevelRequest("Updated", null, null, null);
        var level = new WorkItemLevelDto(1, "Updated", "Crown20", "#7B68EE", 0, true);
        _service.Setup(s => s.UpdateAsync(ProjectId, 1, request)).ReturnsAsync(level);

        var result = await _sut.Update(ProjectId, 1, request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateWorkItemLevelRequest("Updated", null, null, null);
        _service.Setup(s => s.UpdateAsync(ProjectId, 99, request))
            .ReturnsAsync((WorkItemLevelDto?)null);

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
