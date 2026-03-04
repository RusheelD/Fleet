using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace Fleet.Server.Tests.Filters;

[TestClass]
public class ProjectOwnershipFilterTests
{
    private Mock<IAuthService> _authService = null!;
    private Mock<IProjectRepository> _projectRepository = null!;
    private ProjectOwnershipFilter _sut = null!;

    private const int UserId = 42;
    private const string ProjectId = "proj-1";

    [TestInitialize]
    public void Setup()
    {
        _authService = new Mock<IAuthService>();
        _projectRepository = new Mock<IProjectRepository>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new ProjectOwnershipFilter(_authService.Object, _projectRepository.Object);
    }

    [TestMethod]
    public async Task OnActionExecutionAsync_ProjectExists_CallsNext()
    {
        var project = new ProjectDto(ProjectId, "42", "Test", "test", "", "", new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "");
        _projectRepository.Setup(r => r.GetByIdAsync(ProjectId, "42")).ReturnsAsync(project);

        var nextCalled = false;
        var context = CreateContext(ProjectId);
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        };

        await _sut.OnActionExecutionAsync(context, next);

        Assert.IsTrue(nextCalled, "Expected next() to be called when project exists.");
        Assert.IsNull(context.Result);
    }

    [TestMethod]
    public async Task OnActionExecutionAsync_ProjectNotFound_Returns404()
    {
        _projectRepository.Setup(r => r.GetByIdAsync(ProjectId, "42")).ReturnsAsync((ProjectDto?)null);

        var nextCalled = false;
        var context = CreateContext(ProjectId);
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        };

        await _sut.OnActionExecutionAsync(context, next);

        Assert.IsFalse(nextCalled, "Expected next() NOT to be called when project is missing.");
        Assert.IsInstanceOfType<NotFoundResult>(context.Result);
    }

    [TestMethod]
    public async Task OnActionExecutionAsync_NoProjectIdInRoute_CallsNext()
    {
        var nextCalled = false;
        var context = CreateContext(null); // No projectId route value
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        };

        await _sut.OnActionExecutionAsync(context, next);

        Assert.IsTrue(nextCalled, "Expected next() when no projectId in route.");
        Assert.IsNull(context.Result);
    }

    [TestMethod]
    public async Task OnActionExecutionAsync_EmptyProjectId_CallsNext()
    {
        var nextCalled = false;
        var context = CreateContext("   "); // Whitespace projectId
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        };

        await _sut.OnActionExecutionAsync(context, next);

        Assert.IsTrue(nextCalled, "Expected next() when projectId is whitespace.");
        Assert.IsNull(context.Result);
    }

    // ── Helper ───────────────────────────────────────────

    private static ActionExecutingContext CreateContext(string? projectId)
    {
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        if (projectId is not null)
        {
            routeData.Values["projectId"] = projectId;
        }

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }
}
