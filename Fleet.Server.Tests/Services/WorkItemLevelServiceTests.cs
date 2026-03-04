using Fleet.Server.Models;
using Fleet.Server.WorkItems;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class WorkItemLevelServiceTests
{
    private Mock<IWorkItemLevelRepository> _repo = null!;
    private Mock<ILogger<WorkItemLevelService>> _logger = null!;
    private WorkItemLevelService _sut = null!;

    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _repo = new Mock<IWorkItemLevelRepository>();
        _logger = new Mock<ILogger<WorkItemLevelService>>();
        _sut = new WorkItemLevelService(_repo.Object, _logger.Object);
    }

    [TestMethod]
    public async Task GetByProjectIdAsync_DelegatesToRepo()
    {
        var levels = new List<WorkItemLevelDto>
        {
            new(1, "Epic", "Crown20", "#7B68EE", 0, true),
            new(2, "Feature", "Lightbulb", "#4CAF50", 1, true),
        };
        _repo.Setup(r => r.GetByProjectIdAsync(ProjectId)).ReturnsAsync(levels);

        var result = await _sut.GetByProjectIdAsync(ProjectId);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetByIdAsync_Found_ReturnsLevel()
    {
        var level = new WorkItemLevelDto(1, "Epic", "Crown20", "#7B68EE", 0, true);
        _repo.Setup(r => r.GetByIdAsync(ProjectId, 1)).ReturnsAsync(level);

        var result = await _sut.GetByIdAsync(ProjectId, 1);

        Assert.IsNotNull(result);
        Assert.AreEqual("Epic", result.Name);
    }

    [TestMethod]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetByIdAsync(ProjectId, 99)).ReturnsAsync((WorkItemLevelDto?)null);

        var result = await _sut.GetByIdAsync(ProjectId, 99);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CreateAsync_DelegatesToRepo()
    {
        var request = new CreateWorkItemLevelRequest("Sprint", "Timer", "#FF9800", 3);
        var expected = new WorkItemLevelDto(3, "Sprint", "Timer", "#FF9800", 3, false);
        _repo.Setup(r => r.CreateAsync(ProjectId, request)).ReturnsAsync(expected);

        var result = await _sut.CreateAsync(ProjectId, request);

        Assert.AreEqual("Sprint", result.Name);
        Assert.IsFalse(result.IsDefault);
    }

    [TestMethod]
    public async Task UpdateAsync_Found_ReturnsUpdated()
    {
        var request = new UpdateWorkItemLevelRequest("Updated", null, null, null);
        var expected = new WorkItemLevelDto(1, "Updated", "Crown20", "#7B68EE", 0, true);
        _repo.Setup(r => r.UpdateAsync(ProjectId, 1, request)).ReturnsAsync(expected);

        var result = await _sut.UpdateAsync(ProjectId, 1, request);

        Assert.IsNotNull(result);
        Assert.AreEqual("Updated", result.Name);
    }

    [TestMethod]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var request = new UpdateWorkItemLevelRequest("Updated", null, null, null);
        _repo.Setup(r => r.UpdateAsync(ProjectId, 99, request)).ReturnsAsync((WorkItemLevelDto?)null);

        var result = await _sut.UpdateAsync(ProjectId, 99, request);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_Found_ReturnsTrue()
    {
        _repo.Setup(r => r.DeleteAsync(ProjectId, 1)).ReturnsAsync(true);

        var result = await _sut.DeleteAsync(ProjectId, 1);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.DeleteAsync(ProjectId, 99)).ReturnsAsync(false);

        var result = await _sut.DeleteAsync(ProjectId, 99);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task EnsureDefaultLevelsAsync_DelegatesToRepo()
    {
        _repo.Setup(r => r.EnsureDefaultLevelsAsync(ProjectId)).Returns(Task.CompletedTask);

        await _sut.EnsureDefaultLevelsAsync(ProjectId);

        _repo.Verify(r => r.EnsureDefaultLevelsAsync(ProjectId), Times.Once);
    }
}
