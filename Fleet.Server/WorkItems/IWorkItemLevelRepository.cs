using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public interface IWorkItemLevelRepository
{
    Task<IReadOnlyList<WorkItemLevelDto>> GetByProjectIdAsync(string projectId);
    Task<WorkItemLevelDto?> GetByIdAsync(string projectId, int id);
    Task<WorkItemLevelDto> CreateAsync(string projectId, CreateWorkItemLevelRequest request);
    Task<WorkItemLevelDto?> UpdateAsync(string projectId, int id, UpdateWorkItemLevelRequest request);
    Task<bool> DeleteAsync(string projectId, int id);
    Task EnsureDefaultLevelsAsync(string projectId);
}
