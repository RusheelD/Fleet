using Fleet.Server.Models;

namespace Fleet.Server.Projects;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectDto>> GetAllByOwnerAsync(string ownerId);
    Task<ProjectDto?> GetByIdAsync(string id, string ownerId);
    Task<ProjectDto?> GetBySlugAsync(string slug, string ownerId);
    Task<bool> IsSlugAvailableAsync(string slug, string? excludeProjectId = null);
    Task<ProjectDto> CreateAsync(string ownerId, string title, string description, string repo);
    Task<ProjectDto?> UpdateAsync(string id, string ownerId, string? title, string? description, string? repo);
    Task<bool> DeleteAsync(string id, string ownerId);
}
