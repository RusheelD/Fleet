using Fleet.Server.Data;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Projects;

public class ProjectRepository(FleetDbContext context) : IProjectRepository
{
    public async Task<IReadOnlyList<ProjectDto>> GetAllByOwnerAsync(string ownerId)
    {
        var entities = await context.Projects.AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(string id, string ownerId)
    {
        var p = await context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
        return p is null ? null : MapToDto(p);
    }

    public async Task<ProjectDto?> GetBySlugAsync(string slug, string ownerId)
    {
        var p = await context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.OwnerId == ownerId);
        return p is null ? null : MapToDto(p);
    }

    public async Task<bool> IsSlugAvailableAsync(string ownerId, string slug, string? excludeProjectId = null)
    {
        return !await context.Projects.AnyAsync(p =>
            p.OwnerId == ownerId &&
            p.Slug == slug &&
            (excludeProjectId == null || p.Id != excludeProjectId));
    }

    public async Task<ProjectDto> CreateAsync(
        string ownerId,
        string title,
        string description,
        string repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null)
    {
        var slug = SlugHelper.GenerateSlug(title);

        if (!await IsSlugAvailableAsync(ownerId, slug))
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
            BranchPattern = NormalizeBranchPattern(branchPattern),
            CommitAuthorMode = NormalizeCommitAuthorMode(commitAuthorMode),
            CommitAuthorName = string.IsNullOrWhiteSpace(commitAuthorName) ? null : commitAuthorName.Trim(),
            CommitAuthorEmail = string.IsNullOrWhiteSpace(commitAuthorEmail) ? null : commitAuthorEmail.Trim(),
            WorkItemSummary = new Data.Entities.WorkItemSummary(),
            AgentSummary = new Data.Entities.AgentSummary(),
        };

        context.Projects.Add(entity);
        await context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectDto?> UpdateAsync(
        string id,
        string ownerId,
        string? title,
        string? description,
        string? repo,
        string? branchPattern = null,
        string? commitAuthorMode = null,
        string? commitAuthorName = null,
        string? commitAuthorEmail = null)
    {
        var entity = await context.Projects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
        if (entity is null) return null;

        if (title is not null)
        {
            var newSlug = SlugHelper.GenerateSlug(title);
            if (newSlug != entity.Slug && !await IsSlugAvailableAsync(ownerId, newSlug, id))
            {
                throw new InvalidOperationException(
                    $"A project with the slug '{newSlug}' already exists. Choose a different name.");
            }
            entity.Title = title;
            entity.Slug = newSlug;
        }
        if (description is not null) entity.Description = description;
        if (repo is not null) entity.Repo = repo;
        if (branchPattern is not null) entity.BranchPattern = NormalizeBranchPattern(branchPattern);
        if (commitAuthorMode is not null) entity.CommitAuthorMode = NormalizeCommitAuthorMode(commitAuthorMode);
        if (commitAuthorName is not null) entity.CommitAuthorName = string.IsNullOrWhiteSpace(commitAuthorName) ? null : commitAuthorName.Trim();
        if (commitAuthorEmail is not null) entity.CommitAuthorEmail = string.IsNullOrWhiteSpace(commitAuthorEmail) ? null : commitAuthorEmail.Trim();
        entity.LastActivity = DateTime.UtcNow.ToString("o");

        await context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string id, string ownerId)
    {
        var entity = await context.Projects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
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
        p.LastActivity,
        NormalizeBranchPattern(p.BranchPattern),
        NormalizeCommitAuthorMode(p.CommitAuthorMode),
        p.CommitAuthorName,
        p.CommitAuthorEmail
    );

    private static string NormalizeBranchPattern(string? value)
        => string.IsNullOrWhiteSpace(value) ? "fleet/{workItemNumber}-{slug}" : value.Trim();

    private static string NormalizeCommitAuthorMode(string? value)
        => string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "fleet";
}
