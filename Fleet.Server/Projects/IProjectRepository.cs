using Fleet.Server.Models;

namespace Fleet.Server.Projects;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectDto>> GetAllByOwnerAsync(string ownerId);
    Task<ProjectDto?> GetByIdAsync(string id, string ownerId);
    Task<ProjectDto?> GetBySlugAsync(string slug, string ownerId);
    Task<bool> IsSlugAvailableAsync(string ownerId, string slug, string? excludeProjectId = null);
    Task<ProjectDto> CreateAsync(
        string ownerId,
        string title,
        string description,
        string repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null);
    Task<ProjectDto?> UpdateAsync(
        string id,
        string ownerId,
        string? title,
        string? description,
        string? repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null);
    Task<bool> DeleteAsync(string id, string ownerId);
}
