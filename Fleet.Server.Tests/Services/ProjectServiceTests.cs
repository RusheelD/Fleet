using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.GitHub;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.WorkItems;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ProjectServiceTests
{
    private Mock<IProjectRepository> _projectRepo = null!;
    private Mock<IWorkItemRepository> _workItemRepo = null!;
    private Mock<IAgentTaskRepository> _agentTaskRepo = null!;
    private Mock<IGitHubApiService> _gitHubApi = null!;
    private Mock<IAuthService> _authService = null!;
    private Mock<ILogger<ProjectService>> _logger = null!;
    private ProjectService _sut = null!;

    private const int UserId = 42;
    private const string OwnerId = "42";

    [TestInitialize]
    public void Setup()
    {
        _projectRepo = new Mock<IProjectRepository>();
        _workItemRepo = new Mock<IWorkItemRepository>();
        _agentTaskRepo = new Mock<IAgentTaskRepository>();
        _gitHubApi = new Mock<IGitHubApiService>();
        _authService = new Mock<IAuthService>();
        _logger = new Mock<ILogger<ProjectService>>();

        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);

        _sut = new ProjectService(
            _projectRepo.Object,
            _workItemRepo.Object,
            _agentTaskRepo.Object,
            _gitHubApi.Object,
            _authService.Object,
            _logger.Object);
    }

    // ── GetAllProjectsAsync ──────────────────────────────────

    [TestMethod]
    public async Task GetAllProjectsAsync_ReturnsProjectsWithSummaries()
    {
        var projects = new List<ProjectDto>
        {
            new("p1", OwnerId, "Project 1", "project-1", "Desc", "owner/repo",
                new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago"),
        };

        _projectRepo.Setup(r => r.GetAllByOwnerAsync(OwnerId)).ReturnsAsync(projects);
        _workItemRepo.Setup(r => r.GetSummariesByProjectAsync())
            .ReturnsAsync(new Dictionary<string, WorkItemSummaryDto>
            {
                ["p1"] = new(10, 5, 3)
            });
        _agentTaskRepo.Setup(r => r.GetAgentSummariesByProjectAsync())
            .ReturnsAsync(new Dictionary<string, AgentSummaryDto>
            {
                ["p1"] = new(4, 2)
            });

        var result = await _sut.GetAllProjectsAsync();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(10, result[0].WorkItems.Total);
        Assert.AreEqual(5, result[0].WorkItems.Active);
        Assert.AreEqual(3, result[0].WorkItems.Resolved);
        Assert.AreEqual(4, result[0].Agents.Total);
        Assert.AreEqual(2, result[0].Agents.Running);
    }

    [TestMethod]
    public async Task GetAllProjectsAsync_EmptyList_ReturnsEmpty()
    {
        _projectRepo.Setup(r => r.GetAllByOwnerAsync(OwnerId)).ReturnsAsync([]);
        _workItemRepo.Setup(r => r.GetSummariesByProjectAsync())
            .ReturnsAsync(new Dictionary<string, WorkItemSummaryDto>());
        _agentTaskRepo.Setup(r => r.GetAgentSummariesByProjectAsync())
            .ReturnsAsync(new Dictionary<string, AgentSummaryDto>());

        var result = await _sut.GetAllProjectsAsync();

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetAllProjectsAsync_MissingSummaries_DefaultsToZero()
    {
        var projects = new List<ProjectDto>
        {
            new("p1", OwnerId, "Project 1", "project-1", "Desc", "owner/repo",
                new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago"),
        };

        _projectRepo.Setup(r => r.GetAllByOwnerAsync(OwnerId)).ReturnsAsync(projects);
        _workItemRepo.Setup(r => r.GetSummariesByProjectAsync())
            .ReturnsAsync(new Dictionary<string, WorkItemSummaryDto>()); // No summaries
        _agentTaskRepo.Setup(r => r.GetAgentSummariesByProjectAsync())
            .ReturnsAsync(new Dictionary<string, AgentSummaryDto>()); // No summaries

        var result = await _sut.GetAllProjectsAsync();

        Assert.AreEqual(0, result[0].WorkItems.Total);
        Assert.AreEqual(0, result[0].Agents.Total);
    }

    // ── CheckSlugAsync ───────────────────────────────────────

    [TestMethod]
    public async Task CheckSlugAsync_ValidName_ReturnsSlugAndAvailability()
    {
        _projectRepo.Setup(r => r.IsSlugAvailableAsync("my-project")).ReturnsAsync(true);

        var result = await _sut.CheckSlugAsync("My Project");

        Assert.AreEqual("my-project", result.Slug);
        Assert.IsTrue(result.Available);
    }

    [TestMethod]
    public async Task CheckSlugAsync_UnavailableSlug_ReturnsFalse()
    {
        _projectRepo.Setup(r => r.IsSlugAvailableAsync("taken")).ReturnsAsync(false);

        var result = await _sut.CheckSlugAsync("Taken");

        Assert.IsFalse(result.Available);
    }

    [TestMethod]
    public async Task CheckSlugAsync_EmptySlug_ReturnsFalse()
    {
        // Special characters that produce an empty slug
        var result = await _sut.CheckSlugAsync("!!!###");

        Assert.AreEqual(string.Empty, result.Slug);
        Assert.IsFalse(result.Available);
    }

    // ── CreateProjectAsync ───────────────────────────────────

    [TestMethod]
    public async Task CreateProjectAsync_CallsRepoWithOwnerId()
    {
        var expected = new ProjectDto("p1", OwnerId, "New", "new", "Desc", "repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "just now");
        _projectRepo.Setup(r => r.CreateAsync(OwnerId, "New", "Desc", "repo")).ReturnsAsync(expected);

        var result = await _sut.CreateProjectAsync("New", "Desc", "repo");

        Assert.AreEqual("p1", result.Id);
        _projectRepo.Verify(r => r.CreateAsync(OwnerId, "New", "Desc", "repo"), Times.Once);
    }

    // ── UpdateProjectAsync ───────────────────────────────────

    [TestMethod]
    public async Task UpdateProjectAsync_ExistingProject_ReturnsUpdated()
    {
        var expected = new ProjectDto("p1", OwnerId, "Updated", "updated", "New Desc", "repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "just now");
        _projectRepo.Setup(r => r.UpdateAsync("p1", OwnerId, "Updated", "New Desc", "repo"))
            .ReturnsAsync(expected);

        var result = await _sut.UpdateProjectAsync("p1", "Updated", "New Desc", "repo");

        Assert.IsNotNull(result);
        Assert.AreEqual("Updated", result.Title);
    }

    [TestMethod]
    public async Task UpdateProjectAsync_NotFound_ReturnsNull()
    {
        _projectRepo.Setup(r => r.UpdateAsync("missing", OwnerId, null, null, null))
            .ReturnsAsync((ProjectDto?)null);

        var result = await _sut.UpdateProjectAsync("missing", null, null, null);

        Assert.IsNull(result);
    }

    // ── DeleteProjectAsync ───────────────────────────────────

    [TestMethod]
    public async Task DeleteProjectAsync_Existing_ReturnsTrue()
    {
        _projectRepo.Setup(r => r.DeleteAsync("p1", OwnerId)).ReturnsAsync(true);

        var result = await _sut.DeleteProjectAsync("p1");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteProjectAsync_NotFound_ReturnsFalse()
    {
        _projectRepo.Setup(r => r.DeleteAsync("missing", OwnerId)).ReturnsAsync(false);

        var result = await _sut.DeleteProjectAsync("missing");

        Assert.IsFalse(result);
    }

    // ── GetDashboardAsync ────────────────────────────────────

    [TestMethod]
    public async Task GetDashboardAsync_ProjectExists_ReturnsDashboard()
    {
        var project = new ProjectDto("p1", OwnerId, "Project 1", "project-1", "Desc", "owner/repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago");
        _projectRepo.Setup(r => r.GetByIdAsync("p1", OwnerId)).ReturnsAsync(project);
        SetupDashboardDependencies("p1", "owner/repo");

        var result = await _sut.GetDashboardAsync("p1");

        Assert.IsNotNull(result);
        Assert.AreEqual("Project 1", result.Title);
        Assert.AreEqual(5, result.Metrics.Length); // 5 metric cards
    }

    [TestMethod]
    public async Task GetDashboardAsync_NotFound_ReturnsNull()
    {
        _projectRepo.Setup(r => r.GetByIdAsync("missing", OwnerId)).ReturnsAsync((ProjectDto?)null);

        var result = await _sut.GetDashboardAsync("missing");

        Assert.IsNull(result);
    }

    // ── GetDashboardBySlugAsync ──────────────────────────────

    [TestMethod]
    public async Task GetDashboardBySlugAsync_ProjectExists_ReturnsDashboard()
    {
        var project = new ProjectDto("p1", OwnerId, "Project 1", "project-1", "Desc", "owner/repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago");
        _projectRepo.Setup(r => r.GetBySlugAsync("project-1", OwnerId)).ReturnsAsync(project);
        SetupDashboardDependencies("p1", "owner/repo");

        var result = await _sut.GetDashboardBySlugAsync("project-1");

        Assert.IsNotNull(result);
        Assert.AreEqual("project-1", result.Slug);
    }

    [TestMethod]
    public async Task GetDashboardBySlugAsync_NotFound_ReturnsNull()
    {
        _projectRepo.Setup(r => r.GetBySlugAsync("missing", OwnerId)).ReturnsAsync((ProjectDto?)null);

        var result = await _sut.GetDashboardBySlugAsync("missing");

        Assert.IsNull(result);
    }

    // ── Dashboard: GitHub failure fallback ────────────────────

    [TestMethod]
    public async Task GetDashboardAsync_GitHubFails_FallbackToZeros()
    {
        var project = new ProjectDto("p1", OwnerId, "Project 1", "project-1", "Desc", "owner/repo",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago");
        _projectRepo.Setup(r => r.GetByIdAsync("p1", OwnerId)).ReturnsAsync(project);

        _agentTaskRepo.Setup(r => r.GetDashboardAgentsByProjectIdAsync("p1"))
            .ReturnsAsync(new List<DashboardAgentDto>());
        _agentTaskRepo.Setup(r => r.GetAgentSummaryByProjectIdAsync("p1"))
            .ReturnsAsync(new AgentSummaryDto(0, 0));
        _workItemRepo.Setup(r => r.GetByProjectIdAsync("p1"))
            .ReturnsAsync(new List<WorkItemDto>());
        _gitHubApi.Setup(r => r.GetRepoStatsAsync(UserId, "owner/repo"))
            .ThrowsAsync(new Exception("GitHub API down"));

        var result = await _sut.GetDashboardAsync("p1");

        Assert.IsNotNull(result);
        // Should have fallback activity message
        Assert.IsTrue(result.Activities.Length > 0);
    }

    [TestMethod]
    public async Task GetDashboardAsync_NoRepo_SkipsGitHub()
    {
        var project = new ProjectDto("p1", OwnerId, "Project 1", "project-1", "Desc", "",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago");
        _projectRepo.Setup(r => r.GetByIdAsync("p1", OwnerId)).ReturnsAsync(project);

        _agentTaskRepo.Setup(r => r.GetDashboardAgentsByProjectIdAsync("p1"))
            .ReturnsAsync(new List<DashboardAgentDto>());
        _agentTaskRepo.Setup(r => r.GetAgentSummaryByProjectIdAsync("p1"))
            .ReturnsAsync(new AgentSummaryDto(0, 0));
        _workItemRepo.Setup(r => r.GetByProjectIdAsync("p1"))
            .ReturnsAsync(new List<WorkItemDto>());

        var result = await _sut.GetDashboardAsync("p1");

        Assert.IsNotNull(result);
        _gitHubApi.Verify(g => g.GetRepoStatsAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    // ── Dashboard: Work item metrics ─────────────────────────

    [TestMethod]
    public async Task GetDashboardAsync_ComputesWorkItemMetrics()
    {
        var project = new ProjectDto("p1", OwnerId, "Project 1", "project-1", "Desc", "",
            new WorkItemSummaryDto(0, 0, 0), new AgentSummaryDto(0, 0), "1 day ago");
        _projectRepo.Setup(r => r.GetByIdAsync("p1", OwnerId)).ReturnsAsync(project);

        var workItems = new List<WorkItemDto>
        {
            new(1, "Item 1", "Active", 1, 3, "user", [], false, "", null, [], null),
            new(2, "Item 2", "New", 2, 3, "user", [], false, "", null, [], null),
            new(3, "Item 3", "Resolved", 1, 3, "user", [], false, "", null, [], null),
            new(4, "Item 4", "Closed", 1, 3, "user", [], false, "", null, [], null),
        };
        _workItemRepo.Setup(r => r.GetByProjectIdAsync("p1")).ReturnsAsync(workItems);
        _agentTaskRepo.Setup(r => r.GetDashboardAgentsByProjectIdAsync("p1"))
            .ReturnsAsync(new List<DashboardAgentDto>());
        _agentTaskRepo.Setup(r => r.GetAgentSummaryByProjectIdAsync("p1"))
            .ReturnsAsync(new AgentSummaryDto(2, 1));

        var result = await _sut.GetDashboardAsync("p1");

        Assert.IsNotNull(result);
        var totalMetric = result.Metrics.First(m => m.Label == "Total Work Items");
        Assert.AreEqual("4", totalMetric.Value);

        var completionMetric = result.Metrics.First(m => m.Label == "Completion");
        Assert.AreEqual("50%", completionMetric.Value); // 2 out of 4 completed
        Assert.AreEqual(0.5, completionMetric.Progress);
    }

    // ── Helper ───────────────────────────────────────────────

    private void SetupDashboardDependencies(string projectId, string repo)
    {
        _agentTaskRepo.Setup(r => r.GetDashboardAgentsByProjectIdAsync(projectId))
            .ReturnsAsync(new List<DashboardAgentDto>());
        _agentTaskRepo.Setup(r => r.GetAgentSummaryByProjectIdAsync(projectId))
            .ReturnsAsync(new AgentSummaryDto(2, 1));
        _workItemRepo.Setup(r => r.GetByProjectIdAsync(projectId))
            .ReturnsAsync(new List<WorkItemDto>());
        if (!string.IsNullOrEmpty(repo))
        {
            _gitHubApi.Setup(r => r.GetRepoStatsAsync(UserId, repo))
                .ReturnsAsync(new GitHubRepoStats(3, 5, 20, []));
        }
    }
}
