using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public interface IWorkItemService
{
    Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId);
    Task<WorkItemDto?> GetByWorkItemNumberAsync(string projectId, int workItemNumber);
    Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request);
    Task<WorkItemDto?> UpdateAsync(string projectId, int workItemNumber, UpdateWorkItemRequest request);
    Task<bool> DeleteAsync(string projectId, int workItemNumber, CancellationToken cancellationToken = default);
}
