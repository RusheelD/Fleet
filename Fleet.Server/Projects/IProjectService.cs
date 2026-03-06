using Fleet.Server.Models;

namespace Fleet.Server.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync();
    Task<ProjectDashboardDto?> GetDashboardAsync(string projectId);
    Task<ProjectDashboardDto?> GetDashboardBySlugAsync(string slug);
    Task<SlugCheckResult> CheckSlugAsync(string name);
    Task<ProjectDto> CreateProjectAsync(
        string title,
        string description,
        string repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null);
    Task<ProjectDto?> UpdateProjectAsync(
        string id,
        string? title,
        string? description,
        string? repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null);
    Task<bool> DeleteProjectAsync(string id);
}
