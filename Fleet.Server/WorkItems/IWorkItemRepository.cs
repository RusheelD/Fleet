using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public interface IWorkItemRepository
{
    Task<Dictionary<string, WorkItemSummaryDto>> GetSummariesByProjectAsync();
    Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId);
    Task<WorkItemDto?> GetByWorkItemNumberAsync(string projectId, int workItemNumber);
    Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request);
    Task<WorkItemDto?> UpdateAsync(string projectId, int workItemNumber, UpdateWorkItemRequest request);
    Task<bool> DeleteAsync(string projectId, int workItemNumber);

    /// <summary>
    /// Returns work item state counts for a project using a single DB query.
    /// Used by the dashboard to avoid loading all work items into memory.
    /// </summary>
    Task<WorkItemStateCounts> GetStateCountsAsync(string projectId);
}
