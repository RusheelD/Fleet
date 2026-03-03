using Fleet.Server.Data;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Projects;

public class ProjectRepository(FleetDbContext context) : IProjectRepository
{
    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync()
    {
        var entities = await context.Projects.AsNoTracking().ToListAsync();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(string id)
    {
        var p = await context.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? null : MapToDto(p);
    }

    public async Task<ProjectDto?> GetBySlugAsync(string slug)
    {
        var p = await context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug);
        return p is null ? null : MapToDto(p);
    }

    public async Task<bool> IsSlugAvailableAsync(string slug, string? excludeProjectId = null)
    {
        return !await context.Projects.AnyAsync(p =>
            p.Slug == slug && (excludeProjectId == null || p.Id != excludeProjectId));
    }

    public async Task<ProjectDto> CreateAsync(string ownerId, string title, string description, string repo)
    {
        var slug = SlugHelper.GenerateSlug(title);

        if (!await IsSlugAvailableAsync(slug))
        {
            throw new InvalidOperationException(
                $"A project with the slug '{slug}' already exists. Choose a different name.");
        }

        var entity = new Data.Entities.Project
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            OwnerId = ownerId,
            Title = title,
            Slug = slug,
            Description = description,
            Repo = repo,
            LastActivity = DateTime.UtcNow.ToString("o"),
            WorkItemSummary = new Data.Entities.WorkItemSummary(),
            AgentSummary = new Data.Entities.AgentSummary(),
        };

        context.Projects.Add(entity);
        await context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectDto?> UpdateAsync(string id, string? title, string? description, string? repo)
    {
        var entity = await context.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return null;

        if (title is not null)
        {
            var newSlug = SlugHelper.GenerateSlug(title);
            if (newSlug != entity.Slug && !await IsSlugAvailableAsync(newSlug, id))
            {
                throw new InvalidOperationException(
                    $"A project with the slug '{newSlug}' already exists. Choose a different name.");
            }
            entity.Title = title;
            entity.Slug = newSlug;
        }
        if (description is not null) entity.Description = description;
        if (repo is not null) entity.Repo = repo;
        entity.LastActivity = DateTime.UtcNow.ToString("o");

        await context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await context.Projects.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return false;

        context.Projects.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    private static ProjectDto MapToDto(Data.Entities.Project p) => new(
        p.Id,
        p.OwnerId,
        p.Title,
        p.Slug,
        p.Description,
        p.Repo,
        new WorkItemSummaryDto(p.WorkItemSummary.Total, p.WorkItemSummary.Active, p.WorkItemSummary.Resolved),
        new AgentSummaryDto(p.AgentSummary.Total, p.AgentSummary.Running),
        p.LastActivity
    );
}
