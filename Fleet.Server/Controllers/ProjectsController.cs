using Fleet.Server.Models;
using Fleet.Server.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Fleet.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController(IProjectService projectService) : ControllerBase
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
        return NoContent();
    }
}
