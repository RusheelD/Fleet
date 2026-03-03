using Fleet.Server.Models;

namespace Fleet.Server.Projects;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectDto>> GetAllAsync();
    Task<ProjectDto?> GetByIdAsync(string id);
    Task<ProjectDto?> GetBySlugAsync(string slug);
    Task<bool> IsSlugAvailableAsync(string slug, string? excludeProjectId = null);
    Task<ProjectDto> CreateAsync(string ownerId, string title, string description, string repo);
    Task<ProjectDto?> UpdateAsync(string id, string? title, string? description, string? repo);
    Task<bool> DeleteAsync(string id);
}
