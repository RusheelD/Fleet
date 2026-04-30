using Fleet.Server.Auth;
using Fleet.Server.Models;
using Fleet.Server.Projects;
using Fleet.Server.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Text;
using System.Text.Json;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController(
    IProjectService projectService,
    IProjectImportExportService projectImportExportService,
    IAuthService authService,
    IServerEventPublisher eventPublisher) : ControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 5)]
    public async Task<IActionResult> GetAll()
    {
        var projects = await projectService.GetAllProjectsAsync();
        return Ok(projects);
    }

    [HttpGet("{projectId}")]
    [OutputCache(Duration = 5)]
    public async Task<IActionResult> GetDashboard(string projectId)
    {
        var dashboard = await projectService.GetDashboardAsync(projectId);
        if (dashboard is null) return NotFound();
        return Ok(dashboard);
    }

    [HttpGet("{projectId}/branches")]
    public async Task<IActionResult> GetBranches(string projectId, CancellationToken cancellationToken)
    {
        try
        {
            var branches = await projectService.GetRepositoryBranchesAsync(projectId, cancellationToken);
            if (branches is null) return NotFound();
            return Ok(branches);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Unable to load repository branches",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Instance = HttpContext?.Request?.Path.ToString() ?? $"/api/projects/{projectId}/branches",
            });
        }
    }

    [HttpGet("by-slug/{slug}")]
    [OutputCache(Duration = 5)]
    public async Task<IActionResult> GetDashboardBySlug(string slug)
    {
        var dashboard = await projectService.GetDashboardBySlugAsync(slug);
        if (dashboard is null) return NotFound();
        return Ok(dashboard);
    }

    [HttpGet("check-slug")]
    public async Task<IActionResult> CheckSlug([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Name is required.",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext?.Request?.Path.ToString() ?? "/api/projects/check-slug",
            });

        var result = await projectService.CheckSlugAsync(name);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        try
        {
            var project = await projectService.CreateProjectAsync(
                request.Title,
                request.Description,
                request.Repo,
                request.BranchPattern,
                request.CommitAuthorMode,
                request.CommitAuthorName,
                request.CommitAuthorEmail);
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId = project.Id });
            return Created($"/api/projects/{project.Id}", project);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Instance = HttpContext?.Request?.Path.ToString() ?? "/api/projects",
            });
        }
    }

    [HttpPut("{projectId}")]
    public async Task<IActionResult> Update(string projectId, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            var project = await projectService.UpdateProjectAsync(
                projectId,
                request.Title,
                request.Description,
                request.Repo,
                request.BranchPattern,
                request.CommitAuthorMode,
                request.CommitAuthorName,
                request.CommitAuthorEmail);
            if (project is null) return NotFound();
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { projectId });
            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Instance = HttpContext?.Request?.Path.ToString() ?? $"/api/projects/{projectId}",
            });
        }
    }

    [HttpDelete("{projectId}")]
    public async Task<IActionResult> Delete(string projectId)
    {
        var deleted = await projectService.DeleteProjectAsync(projectId);
        if (!deleted) return NotFound();
        var userId = await authService.GetCurrentUserIdAsync();
        await eventPublisher.PublishUserEventAsync(
            userId,
            ServerEventTopics.ProjectsUpdated,
            new { projectId });
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var payload = await projectImportExportService.ExportProjectsAsync(cancellationToken);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"fleet-projects-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(bytes, "application/json", fileName);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ProjectsExportFileDto payload, CancellationToken cancellationToken)
    {
        try
        {
            var importResult = await projectImportExportService.ImportProjectsAsync(payload, cancellationToken);
            var userId = await authService.GetCurrentUserIdAsync();
            await eventPublisher.PublishUserEventAsync(
                userId,
                ServerEventTopics.ProjectsUpdated,
                new { importedProjectIds = importResult.ImportedProjectIds });
            return Ok(importResult);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid import file",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext?.Request?.Path.ToString() ?? "/api/projects/import",
            });
        }
    }
}
