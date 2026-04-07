using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class WorkItemServiceTests
{
    private Mock<IWorkItemRepository> _repo = null!;
    private Mock<IWorkItemAttachmentService> _attachmentService = null!;
    private Mock<ILogger<WorkItemService>> _logger = null!;
    private WorkItemService _sut = null!;

    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _repo = new Mock<IWorkItemRepository>();
        _attachmentService = new Mock<IWorkItemAttachmentService>();
        _logger = new Mock<ILogger<WorkItemService>>();
        _sut = new WorkItemService(_repo.Object, _attachmentService.Object, _logger.Object);
    }

    [TestMethod]
    public async Task GetByProjectIdAsync_DelegatesToRepo()
    {
        var items = new List<WorkItemDto>
        {
            new(1, "Task 1", "Active", 1, 3, "user", [], false, "desc", null, [], null),
        };
        _repo.Setup(r => r.GetByProjectIdAsync(ProjectId)).ReturnsAsync(items);

        var result = await _sut.GetByProjectIdAsync(ProjectId);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Task 1", result[0].Title);
    }

    [TestMethod]
    public async Task GetByWorkItemNumberAsync_Found_ReturnsItem()
    {
        var item = new WorkItemDto(1, "Task 1", "Active", 1, 3, "user", [], false, "desc", null, [], null);
        _repo.Setup(r => r.GetByWorkItemNumberAsync(ProjectId, 1)).ReturnsAsync(item);

        var result = await _sut.GetByWorkItemNumberAsync(ProjectId, 1);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.WorkItemNumber);
    }

    [TestMethod]
    public async Task GetByWorkItemNumberAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetByWorkItemNumberAsync(ProjectId, 99)).ReturnsAsync((WorkItemDto?)null);

        var result = await _sut.GetByWorkItemNumberAsync(ProjectId, 99);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CreateAsync_DelegatesToRepo()
    {
        var request = new CreateWorkItemRequest("New Task", "Description", 1, 3, "New", "user", [], false, null, null);
        var expected = new WorkItemDto(1, "New Task", "New", 1, 3, "user", [], false, "Description", null, [], null);
        _repo.Setup(r => r.CreateAsync(ProjectId, request)).ReturnsAsync(expected);

        var result = await _sut.CreateAsync(ProjectId, request);

        Assert.AreEqual("New Task", result.Title);
        _repo.Verify(r => r.CreateAsync(ProjectId, request), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAsync_Found_ReturnsUpdated()
    {
        var request = new UpdateWorkItemRequest("Updated", null, null, null, null, null, null, null, null, null);
        var expected = new WorkItemDto(1, "Updated", "Active", 1, 3, "user", [], false, "desc", null, [], null);
        _repo.Setup(r => r.UpdateAsync(ProjectId, 1, request)).ReturnsAsync(expected);

        var result = await _sut.UpdateAsync(ProjectId, 1, request);

        Assert.IsNotNull(result);
        Assert.AreEqual("Updated", result.Title);
    }

    [TestMethod]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var request = new UpdateWorkItemRequest("Updated", null, null, null, null, null, null, null, null, null);
        _repo.Setup(r => r.UpdateAsync(ProjectId, 99, request)).ReturnsAsync((WorkItemDto?)null);

        var result = await _sut.UpdateAsync(ProjectId, 99, request);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_Found_ReturnsTrue()
    {
        _repo.Setup(r => r.DeleteAsync(ProjectId, 1)).ReturnsAsync(true);

        var result = await _sut.DeleteAsync(ProjectId, 1);

        Assert.IsTrue(result);
        _attachmentService.Verify(s => s.DeleteAllAsync(ProjectId, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.DeleteAsync(ProjectId, 99)).ReturnsAsync(false);

        var result = await _sut.DeleteAsync(ProjectId, 99);

        Assert.IsFalse(result);
    }
}
