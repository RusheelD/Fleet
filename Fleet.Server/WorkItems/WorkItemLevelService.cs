using Fleet.Server.Models;
using Fleet.Server.Logging;

namespace Fleet.Server.WorkItems;

public class WorkItemLevelService(
    IWorkItemLevelRepository workItemLevelRepository,
    ILogger<WorkItemLevelService> logger) : IWorkItemLevelService
{
    public async Task<IReadOnlyList<WorkItemLevelDto>> GetByProjectIdAsync(string projectId)
    {
        logger.WorkItemLevelsRetrieving(projectId.SanitizeForLogging());
        return await workItemLevelRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<WorkItemLevelDto?> GetByIdAsync(string projectId, int id)
    {
        logger.WorkItemLevelsRetrievingById(projectId.SanitizeForLogging(), id);
        return await workItemLevelRepository.GetByIdAsync(projectId, id);
    }

    public async Task<WorkItemLevelDto> CreateAsync(string projectId, CreateWorkItemLevelRequest request)
    {
        logger.WorkItemLevelsCreating(projectId.SanitizeForLogging(), request.Name.SanitizeForLogging());
        return await workItemLevelRepository.CreateAsync(projectId, request);
    }

    public async Task<WorkItemLevelDto?> UpdateAsync(string projectId, int id, UpdateWorkItemLevelRequest request)
    {
        logger.WorkItemLevelsUpdating(projectId.SanitizeForLogging(), id);
        return await workItemLevelRepository.UpdateAsync(projectId, id, request);
    }

    public async Task<bool> DeleteAsync(string projectId, int id)
    {
        logger.WorkItemLevelsDeleting(projectId.SanitizeForLogging(), id);
        return await workItemLevelRepository.DeleteAsync(projectId, id);
    }

    public async Task EnsureDefaultLevelsAsync(string projectId)
    {
        logger.WorkItemLevelsEnsuringDefaults(projectId.SanitizeForLogging());
        await workItemLevelRepository.EnsureDefaultLevelsAsync(projectId);
    }
}
