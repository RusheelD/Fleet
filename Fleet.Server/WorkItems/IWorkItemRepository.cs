using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public interface IWorkItemRepository
{
    Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId);
    Task<WorkItemDto?> GetByIdAsync(string projectId, int id);
    Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request);
    Task<WorkItemDto?> UpdateAsync(string projectId, int id, UpdateWorkItemRequest request);
    Task<bool> DeleteAsync(string projectId, int id);
}
