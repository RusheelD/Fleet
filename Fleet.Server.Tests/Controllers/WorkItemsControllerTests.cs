using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.Realtime;
using Fleet.Server.WorkItems;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class WorkItemsControllerTests
{
    private Mock<IWorkItemService> _service = null!;
    private Mock<IWorkItemAttachmentService> _attachmentService = null!;
    private Mock<IProjectImportExportService> _projectImportExportService = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private WorkItemsController _sut = null!;

    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _service = new Mock<IWorkItemService>();
        _attachmentService = new Mock<IWorkItemAttachmentService>();
        _projectImportExportService = new Mock<IProjectImportExportService>();
        _authService = new Mock<IAuthService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        _sut = new WorkItemsController(
            _service.Object,
            _attachmentService.Object,
            _projectImportExportService.Object,
            _authService.Object,
            _eventPublisher.Object);
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
        _service.Setup(s => s.DeleteAsync(ProjectId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.Delete(ProjectId, 1);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task Delete_NotFound_Returns404()
    {
        _service.Setup(s => s.DeleteAsync(ProjectId, 99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.Delete(ProjectId, 99);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task GetAttachments_ReturnsOk()
    {
        _attachmentService
            .Setup(s => s.GetByWorkItemNumberAsync(ProjectId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WorkItemAttachmentDto("asset-1", "mock.png", 123, DateTime.UtcNow.ToString("o"), "image/png", "/content", "![mock](/content)", true),
            ]);

        var result = await _sut.GetAttachments(ProjectId, 7, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Export_ReturnsJsonFile()
    {
        _projectImportExportService
            .Setup(s => s.ExportWorkItemsAsync(ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectWorkItemsExportFileDto(
                "fleet.workitems",
                1,
                DateTime.UtcNow.ToString("o"),
                "Project 1",
                "owner/repo",
                [],
                []));

        var result = await _sut.Export(ProjectId, CancellationToken.None);

        var file = result as FileContentResult;
        Assert.IsNotNull(file);
        Assert.AreEqual("application/json", file.ContentType);
    }

    [TestMethod]
    public async Task Import_Success_ReturnsOk()
    {
        var payload = new ProjectWorkItemsExportFileDto(
            "fleet.workitems",
            1,
            DateTime.UtcNow.ToString("o"),
            "Project 1",
            "owner/repo",
            [],
            []);
        _projectImportExportService
            .Setup(s => s.ImportWorkItemsAsync(ProjectId, payload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemsImportResultDto(4, 1));

        var result = await _sut.Import(ProjectId, payload, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Import_InvalidPayload_ReturnsBadRequest()
    {
        var payload = new ProjectWorkItemsExportFileDto(
            "bad-format",
            1,
            DateTime.UtcNow.ToString("o"),
            "Project 1",
            "owner/repo",
            [],
            []);
        _projectImportExportService
            .Setup(s => s.ImportWorkItemsAsync(ProjectId, payload, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unsupported work-items import format."));

        var result = await _sut.Import(ProjectId, payload, CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }
}
