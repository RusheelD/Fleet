using Fleet.Server.Models;
using Fleet.Server.Logging;

namespace Fleet.Server.WorkItems;

public class WorkItemService(
    IWorkItemRepository workItemRepository,
    ILogger<WorkItemService> logger) : IWorkItemService
{
    public async Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId
        });

        logger.WorkItemsRetrieving(projectId.SanitizeForLogging());
        return await workItemRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<WorkItemDto?> GetByIdAsync(string projectId, int id)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["WorkItemId"] = id
        });

        logger.WorkItemsRetrievingById(projectId.SanitizeForLogging(), id);
        return await workItemRepository.GetByIdAsync(projectId, id);
    }

    public async Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId
        });

        logger.WorkItemsCreating(projectId.SanitizeForLogging(), request.Title.SanitizeForLogging());
        return await workItemRepository.CreateAsync(projectId, request);
    }

    public async Task<WorkItemDto?> UpdateAsync(string projectId, int id, UpdateWorkItemRequest request)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["WorkItemId"] = id
        });

        logger.WorkItemsUpdating(projectId.SanitizeForLogging(), id);
        return await workItemRepository.UpdateAsync(projectId, id, request);
    }

    public async Task<bool> DeleteAsync(string projectId, int id)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["WorkItemId"] = id
        });

        logger.WorkItemsDeleting(projectId.SanitizeForLogging(), id);
        return await workItemRepository.DeleteAsync(projectId, id);
    }
}
