using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public class WorkItemService(
    IWorkItemRepository workItemRepository,
    ILogger<WorkItemService> logger) : IWorkItemService
{
    public async Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId)
    {
        logger.LogInformation("Retrieving work items for project {ProjectId}", projectId);
        return await workItemRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<WorkItemDto?> GetByIdAsync(string projectId, int id)
    {
        logger.LogInformation("Retrieving work item {WorkItemId} for project {ProjectId}", id, projectId);
        return await workItemRepository.GetByIdAsync(projectId, id);
    }

    public async Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request)
    {
        logger.LogInformation("Creating work item in project {ProjectId} with title: {Title}", projectId, request.Title);
        return await workItemRepository.CreateAsync(projectId, request);
    }

    public async Task<WorkItemDto?> UpdateAsync(string projectId, int id, UpdateWorkItemRequest request)
    {
        logger.LogInformation("Updating work item {WorkItemId} in project {ProjectId}", id, projectId);
        return await workItemRepository.UpdateAsync(projectId, id, request);
    }

    public async Task<bool> DeleteAsync(string projectId, int id)
    {
        logger.LogInformation("Deleting work item {WorkItemId} from project {ProjectId}", id, projectId);
        return await workItemRepository.DeleteAsync(projectId, id);
    }
}
