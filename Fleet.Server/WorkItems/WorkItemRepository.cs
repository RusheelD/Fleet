using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.WorkItems;

public class WorkItemRepository(FleetDbContext context) : IWorkItemRepository
{
    public async Task<IReadOnlyList<WorkItemDto>> GetByProjectIdAsync(string projectId)
    {
        var entities = await context.WorkItems
            .AsNoTracking()
            .Include(w => w.Children)
            .Where(w => w.ProjectId == projectId)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<WorkItemDto?> GetByIdAsync(string projectId, int id)
    {
        var entity = await context.WorkItems
            .AsNoTracking()
            .Include(w => w.Children)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.Id == id);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<WorkItemDto> CreateAsync(string projectId, CreateWorkItemRequest request)
    {
        // Determine next ID for this project
        var maxId = await context.WorkItems
            .Where(w => w.ProjectId == projectId)
            .MaxAsync(w => (int?)w.Id) ?? 0;

        var entity = new WorkItem
        {
            Id = maxId + 1,
            Title = request.Title,
            Description = request.Description,
            State = request.State,
            Priority = request.Priority,
            AssignedTo = request.AssignedTo,
            Tags = [.. request.Tags],
            IsAI = request.IsAI,
            ParentId = request.ParentId,
            LevelId = request.LevelId,
            ProjectId = projectId,
        };

        context.WorkItems.Add(entity);
        await context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<WorkItemDto?> UpdateAsync(string projectId, int id, UpdateWorkItemRequest request)
    {
        var entity = await context.WorkItems
            .Include(w => w.Children)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.Id == id);

        if (entity is null) return null;

        if (request.Title is not null) entity.Title = request.Title;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.State is not null) entity.State = request.State;
        if (request.Priority is not null) entity.Priority = request.Priority.Value;
        if (request.AssignedTo is not null) entity.AssignedTo = request.AssignedTo;
        if (request.Tags is not null) entity.Tags = [.. request.Tags];
        if (request.IsAI is not null) entity.IsAI = request.IsAI.Value;

        // ParentId & LevelId: 0 = clear (set to null), null = don't change, >0 = set value
        if (request.ParentId is not null)
            entity.ParentId = request.ParentId == 0 ? null : request.ParentId;
        if (request.LevelId is not null)
            entity.LevelId = request.LevelId == 0 ? null : request.LevelId;

        await context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string projectId, int id)
    {
        var entity = await context.WorkItems
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.Id == id);

        if (entity is null) return false;

        context.WorkItems.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    private static WorkItemDto MapToDto(WorkItem w) => new(
        w.Id, w.Title, w.State, w.Priority, w.AssignedTo,
        [.. w.Tags], w.IsAI, w.Description,
        w.ParentId,
        w.Children.Select(c => c.Id).ToArray(),
        w.LevelId
    );
}
