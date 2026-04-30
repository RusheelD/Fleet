using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class ProjectsControllerTests
{
    private Mock<IProjectService> _projectService = null!;
    private Mock<IProjectImportExportService> _projectImportExportService = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<IServerEventPublisher> _eventPublisher = null!;
    private ProjectsController _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _projectService = new Mock<IProjectService>();
        _projectImportExportService = new Mock<IProjectImportExportService>();
        _authService = new Mock<IAuthService>();
        _eventPublisher = new Mock<IServerEventPublisher>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(42);
        _sut = new ProjectsController(
            _projectService.Object,
            _projectImportExportService.Object,
            _authService.Object,
            _eventPublisher.Object);
    }

    [TestMethod]
    public async Task GetAll_ReturnsOkWithProjects()
    {
        var projects = new List<ProjectDto>
        {
            new("p1", "42", "Project 1", "project-1", "Desc", "repo",
                new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago")
        };
        _projectService.Setup(s => s.GetAllProjectsAsync()).ReturnsAsync(projects);

        var result = await _sut.GetAll();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreEqual(200, ok.StatusCode);
    }

    [TestMethod]
    public async Task GetDashboard_Found_ReturnsOk()
    {
        var dashboard = new ProjectDashboardDto("p1", "slug", "Title", "repo", [], [], []);
        _projectService.Setup(s => s.GetDashboardAsync("p1")).ReturnsAsync(dashboard);

        var result = await _sut.GetDashboard("p1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetDashboard_NotFound_Returns404()
    {
        _projectService.Setup(s => s.GetDashboardAsync("missing")).ReturnsAsync((ProjectDashboardDto?)null);

        var result = await _sut.GetDashboard("missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task GetBranches_Found_ReturnsOk()
    {
        _projectService
            .Setup(s => s.GetRepositoryBranchesAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProjectBranchDto("main", true, false, true),
            ]);

        var result = await _sut.GetBranches("p1", CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
    }

    [TestMethod]
    public async Task GetBranches_NotFound_Returns404()
    {
        _projectService
            .Setup(s => s.GetRepositoryBranchesAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProjectBranchDto>?)null);

        var result = await _sut.GetBranches("missing", CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task GetDashboardBySlug_Found_ReturnsOk()
    {
        var dashboard = new ProjectDashboardDto("p1", "slug", "Title", "repo", [], [], []);
        _projectService.Setup(s => s.GetDashboardBySlugAsync("slug")).ReturnsAsync(dashboard);

        var result = await _sut.GetDashboardBySlug("slug");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetDashboardBySlug_NotFound_Returns404()
    {
        _projectService.Setup(s => s.GetDashboardBySlugAsync("missing")).ReturnsAsync((ProjectDashboardDto?)null);

        var result = await _sut.GetDashboardBySlug("missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task CheckSlug_EmptyName_ReturnsBadRequest()
    {
        var result = await _sut.CheckSlug("");

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task CheckSlug_ValidName_ReturnsOk()
    {
        var slugResult = new SlugCheckResult("my-project", true);
        _projectService.Setup(s => s.CheckSlugAsync("My Project")).ReturnsAsync(slugResult);

        var result = await _sut.CheckSlug("My Project");

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
    }

    [TestMethod]
    public async Task Create_Success_ReturnsCreated()
    {
        var project = new ProjectDto("p1", "42", "New", "new", "Desc", "repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "just now");
        _projectService.Setup(s => s.CreateProjectAsync("New", "Desc", "repo")).ReturnsAsync(project);

        var result = await _sut.Create(new CreateProjectRequest("New", "Desc", "repo"));

        var created = result as CreatedResult;
        Assert.IsNotNull(created);
        Assert.AreEqual(201, created.StatusCode);
    }

    [TestMethod]
    public async Task Create_Conflict_Returns409()
    {
        _projectService.Setup(s => s.CreateProjectAsync("Dup", "Desc", "repo"))
            .ThrowsAsync(new InvalidOperationException("Slug already taken"));

        var result = await _sut.Create(new CreateProjectRequest("Dup", "Desc", "repo"));

        var conflict = result as ConflictObjectResult;
        Assert.IsNotNull(conflict);
        Assert.AreEqual(409, conflict.StatusCode);
    }

    [TestMethod]
    public async Task Update_Found_ReturnsOk()
    {
        var project = new ProjectDto("p1", "42", "Updated", "updated", "Desc", "repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "just now");
        _projectService.Setup(s => s.UpdateProjectAsync("p1", "Updated", "Desc", "repo"))
            .ReturnsAsync(project);

        var result = await _sut.Update("p1", new UpdateProjectRequest("Updated", "Desc", "repo"));

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Update_NotFound_Returns404()
    {
        _projectService.Setup(s => s.UpdateProjectAsync("missing", "Title", null, null))
            .ReturnsAsync((ProjectDto?)null);

        var result = await _sut.Update("missing", new UpdateProjectRequest("Title", null, null));

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task Update_Conflict_Returns409()
    {
        _projectService.Setup(s => s.UpdateProjectAsync("p1", "Dup", null, null))
            .ThrowsAsync(new InvalidOperationException("Slug taken"));

        var result = await _sut.Update("p1", new UpdateProjectRequest("Dup", null, null));

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task Delete_Found_ReturnsNoContent()
    {
        _projectService.Setup(s => s.DeleteProjectAsync("p1")).ReturnsAsync(true);

        var result = await _sut.Delete("p1");

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task Delete_NotFound_Returns404()
    {
        _projectService.Setup(s => s.DeleteProjectAsync("missing")).ReturnsAsync(false);

        var result = await _sut.Delete("missing");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task Export_ReturnsJsonFile()
    {
        _projectImportExportService
            .Setup(s => s.ExportProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectsExportFileDto("fleet.projects+workitems", 1, DateTime.UtcNow.ToString("o"), []));

        var result = await _sut.Export(CancellationToken.None);

        var file = result as FileContentResult;
        Assert.IsNotNull(file);
        Assert.AreEqual("application/json", file.ContentType);
    }

    [TestMethod]
    public async Task Import_Success_ReturnsOk()
    {
        var payload = new ProjectsExportFileDto("fleet.projects+workitems", 1, DateTime.UtcNow.ToString("o"), []);
        _projectImportExportService
            .Setup(s => s.ImportProjectsAsync(payload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectsImportResultDto(1, 0, 0, ["p1"]));

        var result = await _sut.Import(payload, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task Import_InvalidPayload_ReturnsBadRequest()
    {
        var payload = new ProjectsExportFileDto("bad-format", 1, DateTime.UtcNow.ToString("o"), []);
        _projectImportExportService
            .Setup(s => s.ImportProjectsAsync(payload, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unsupported projects import format."));

        var result = await _sut.Import(payload, CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }
}
