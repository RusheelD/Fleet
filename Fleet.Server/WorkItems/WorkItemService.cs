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

    public async Task<WorkItemDto?> GetByWorkItemNumberAsync(string projectId, int workItemNumber)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["WorkItemNumber"] = workItemNumber
        });

        logger.WorkItemsRetrievingById(projectId.SanitizeForLogging(), workItemNumber);
        return await workItemRepository.GetByWorkItemNumberAsync(projectId, workItemNumber);
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

    public async Task<WorkItemDto?> UpdateAsync(string projectId, int workItemNumber, UpdateWorkItemRequest request)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["WorkItemNumber"] = workItemNumber
        });

        logger.WorkItemsUpdating(projectId.SanitizeForLogging(), workItemNumber);
        return await workItemRepository.UpdateAsync(projectId, workItemNumber, request);
    }

    public async Task<bool> DeleteAsync(string projectId, int workItemNumber)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProjectId"] = projectId,
            ["WorkItemNumber"] = workItemNumber
        });

        logger.WorkItemsDeleting(projectId.SanitizeForLogging(), workItemNumber);
        return await workItemRepository.DeleteAsync(projectId, workItemNumber);
    }
}
