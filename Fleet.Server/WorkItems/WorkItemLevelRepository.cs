using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.WorkItems;

public class WorkItemLevelRepository(FleetDbContext context) : IWorkItemLevelRepository
{
    /// <summary>
    /// Default levels seeded for every new project.
    /// Distinct from Azure DevOps names by design.
    /// </summary>
    private static readonly (string Name, string Icon, string Color, int Ordinal)[] DefaultLevels =
    [
        ("Domain",    "globe",        "#8764B8", 0),
        ("Module",    "puzzle-piece", "#0078D4", 1),
        ("Feature",   "lightbulb",    "#00B7C3", 2),
        ("Component", "code",         "#498205", 3),
        ("Bug",       "bug",          "#D13438", 4),
        ("Task",      "task-list",    "#8A8886", 5),
    ];

    public async Task<IReadOnlyList<WorkItemLevelDto>> GetByProjectIdAsync(string projectId)
    {
        var entities = await context.WorkItemLevels
            .AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .OrderBy(l => l.Ordinal)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<WorkItemLevelDto?> GetByIdAsync(string projectId, int id)
    {
        var entity = await context.WorkItemLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ProjectId == projectId && l.Id == id);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<WorkItemLevelDto> CreateAsync(string projectId, CreateWorkItemLevelRequest request)
    {
        var entity = new WorkItemLevel
        {
            Name = request.Name,
            IconName = request.IconName,
            Color = request.Color,
            Ordinal = request.Ordinal,
            IsDefault = false,
            ProjectId = projectId,
        };

        context.WorkItemLevels.Add(entity);
        await context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<WorkItemLevelDto?> UpdateAsync(string projectId, int id, UpdateWorkItemLevelRequest request)
    {
        var entity = await context.WorkItemLevels
            .FirstOrDefaultAsync(l => l.ProjectId == projectId && l.Id == id);

        if (entity is null) return null;

        if (request.Name is not null) entity.Name = request.Name;
        if (request.IconName is not null) entity.IconName = request.IconName;
        if (request.Color is not null) entity.Color = request.Color;
        if (request.Ordinal is not null) entity.Ordinal = request.Ordinal.Value;

        await context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string projectId, int id)
    {
        var entity = await context.WorkItemLevels
            .FirstOrDefaultAsync(l => l.ProjectId == projectId && l.Id == id);

        if (entity is null) return false;

        context.WorkItemLevels.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task EnsureDefaultLevelsAsync(string projectId)
    {
        var hasLevels = await context.WorkItemLevels
            .AnyAsync(l => l.ProjectId == projectId);

        if (hasLevels) return;

        foreach (var (name, icon, color, ordinal) in DefaultLevels)
        {
            context.WorkItemLevels.Add(new WorkItemLevel
            {
                Name = name,
                IconName = icon,
                Color = color,
                Ordinal = ordinal,
                IsDefault = true,
                ProjectId = projectId,
            });
        }

        await context.SaveChangesAsync();
    }

    private static WorkItemLevelDto MapToDto(WorkItemLevel l) => new(
        l.Id, l.Name, l.IconName, l.Color, l.Ordinal, l.IsDefault
    );
}
