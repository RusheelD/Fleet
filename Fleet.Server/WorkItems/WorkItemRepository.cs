using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.WorkItems;

public class WorkItemRepository(FleetDbContext context) : IWorkItemRepository
{
    public async Task<Dictionary<string, WorkItemSummaryDto>> GetSummariesByProjectAsync()
    {
        var summaries = await context.WorkItems
            .AsNoTracking()
            .GroupBy(w => w.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Active = g.Count(w =>
                    w.State == "Active" || w.State == "New"),
                Resolved = g.Count(w =>
                    w.State == "Resolved" || w.State == "Resolved (AI)"),
            })
            .ToListAsync();

        return summaries.ToDictionary(
            s => s.ProjectId,
            s => new WorkItemSummaryDto(s.Total, s.Active, s.Resolved));
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId)
    {
        var entities = await context.WorkItems
            .AsNoTracking()
            .Include(w => w.Children)
            .Include(w => w.Parent)
            .Where(w => w.ProjectId == projectId)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<WorkItemDto?> GetByWorkItemNumberAsync(string projectId, int workItemNumber)
    {
        var entity = await context.WorkItems
            .AsNoTracking()
            .Include(w => w.Children)
            .Include(w => w.Parent)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkItemNumber == workItemNumber);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request)
    {
        // Determine next project-scoped WorkItemNumber
        var maxNumber = await context.WorkItems
            .Where(w => w.ProjectId == projectId)
            .MaxAsync(w => (int?)w.WorkItemNumber) ?? 0;

        // Resolve parent WorkItemNumber to DB Id
        int? parentDbId = null;
        if (request.ParentWorkItemNumber is not null)
        {
            var parentEntity = await context.WorkItems
                .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkItemNumber == request.ParentWorkItemNumber);
            parentDbId = parentEntity?.Id;
        }

        var entity = new WorkItem
        {
            WorkItemNumber = maxNumber + 1,
            Title = request.Title,
            Description = request.Description,
            State = request.State,
            Priority = request.Priority,
            AssignedTo = request.AssignedTo,
            Tags = [.. request.Tags],
            IsAI = request.IsAI,
            ParentId = parentDbId,
            LevelId = request.LevelId,
            ProjectId = projectId,
        };

        context.WorkItems.Add(entity);
        await context.SaveChangesAsync();

        return new WorkItemDto(
            entity.WorkItemNumber, entity.Title, entity.State, entity.Priority, entity.AssignedTo,
            [.. entity.Tags], entity.IsAI, entity.Description,
            request.ParentWorkItemNumber,
            [],
            entity.LevelId
        );
    }

    public async Task<WorkItemDto?> UpdateAsync(string projectId, int workItemNumber, UpdateWorkItemRequest request)
    {
        var entity = await context.WorkItems
            .Include(w => w.Children)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkItemNumber == workItemNumber);

        if (entity is null) return null;

        if (request.Title is not null) entity.Title = request.Title;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.State is not null) entity.State = request.State;
        if (request.Priority is not null) entity.Priority = request.Priority.Value;
        if (request.AssignedTo is not null) entity.AssignedTo = request.AssignedTo;
        if (request.Tags is not null) entity.Tags = [.. request.Tags];
        if (request.IsAI is not null) entity.IsAI = request.IsAI.Value;

        // ParentWorkItemNumber: 0 = clear (set to null), null = don't change, >0 = set by resolving WorkItemNumber
        if (request.ParentWorkItemNumber is not null)
        {
            if (request.ParentWorkItemNumber == 0)
            {
                entity.ParentId = null;
            }
            else
            {
                var parentEntity = await context.WorkItems
                    .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkItemNumber == request.ParentWorkItemNumber);
                entity.ParentId = parentEntity?.Id;
            }
        }

        if (request.LevelId is not null)
            entity.LevelId = request.LevelId == 0 ? null : request.LevelId;

        await context.SaveChangesAsync();

        // Reload parent navigation to map WorkItemNumber
        await context.Entry(entity).Reference(e => e.Parent).LoadAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string projectId, int workItemNumber)
    {
        var entity = await context.WorkItems
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkItemNumber == workItemNumber);

        if (entity is null) return false;

        context.WorkItems.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    private static WorkItemDto MapToDto(WorkItem w) => new(
        w.WorkItemNumber, w.Title, w.State, w.Priority, w.AssignedTo,
        [.. w.Tags], w.IsAI, w.Description,
        w.Parent?.WorkItemNumber,
        w.Children.Select(c => c.WorkItemNumber).ToArray(),
        w.LevelId
    );
}
