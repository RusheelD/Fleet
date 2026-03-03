using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public class WorkItemLevelService(
    IWorkItemLevelRepository workItemLevelRepository,
    ILogger<WorkItemLevelService> logger) : IWorkItemLevelService
{
    public async Task<IReadOnlyList<WorkItemLevelDto>> GetByProjectIdAsync(string projectId)
    {
        logger.LogInformation("Retrieving work item levels for project {ProjectId}", projectId);
        return await workItemLevelRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<WorkItemLevelDto?> GetByIdAsync(string projectId, int id)
    {
        logger.LogInformation("Retrieving work item level {LevelId} for project {ProjectId}", id, projectId);
        return await workItemLevelRepository.GetByIdAsync(projectId, id);
    }

    public async Task<WorkItemLevelDto> CreateAsync(string projectId, CreateWorkItemLevelRequest request)
    {
        logger.LogInformation("Creating work item level in project {ProjectId} with name: {Name}", projectId, request.Name);
        return await workItemLevelRepository.CreateAsync(projectId, request);
    }

    public async Task<WorkItemLevelDto?> UpdateAsync(string projectId, int id, UpdateWorkItemLevelRequest request)
    {
        logger.LogInformation("Updating work item level {LevelId} in project {ProjectId}", id, projectId);
        return await workItemLevelRepository.UpdateAsync(projectId, id, request);
    }

    public async Task<bool> DeleteAsync(string projectId, int id)
    {
        logger.LogInformation("Deleting work item level {LevelId} from project {ProjectId}", id, projectId);
        return await workItemLevelRepository.DeleteAsync(projectId, id);
    }

    public async Task EnsureDefaultLevelsAsync(string projectId)
    {
        logger.LogInformation("Ensuring default levels exist for project {ProjectId}", projectId);
        await workItemLevelRepository.EnsureDefaultLevelsAsync(projectId);
    }
}
