using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Memories;

public class MemoryRepository(FleetDbContext context) : IMemoryRepository
{
    public async Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(int userId, CancellationToken cancellationToken = default)
        => await context.MemoryEntries
            .AsNoTracking()
            .Where(memory => memory.UserProfileId == userId && memory.ProjectId == null)
            .OrderByDescending(memory => memory.AlwaysInclude)
            .ThenByDescending(memory => memory.UpdatedAtUtc)
            .ThenBy(memory => memory.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MemoryEntry>> GetProjectMemoriesAsync(int userId, string projectId, CancellationToken cancellationToken = default)
        => await context.MemoryEntries
            .AsNoTracking()
            .Where(memory => memory.UserProfileId == userId && memory.ProjectId == projectId)
            .OrderByDescending(memory => memory.AlwaysInclude)
            .ThenByDescending(memory => memory.UpdatedAtUtc)
            .ThenBy(memory => memory.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MemoryEntry>> GetPromptMemoriesAsync(int userId, string? projectId, CancellationToken cancellationToken = default)
    {
        return await context.MemoryEntries
            .AsNoTracking()
            .Where(memory =>
                memory.UserProfileId == userId &&
                (memory.ProjectId == null || memory.ProjectId == projectId))
            .OrderByDescending(memory => memory.ProjectId == projectId)
            .ThenByDescending(memory => memory.AlwaysInclude)
            .ThenByDescending(memory => memory.UpdatedAtUtc)
            .ThenBy(memory => memory.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<MemoryEntry?> GetUserMemoryAsync(int userId, int memoryId, CancellationToken cancellationToken = default)
        => context.MemoryEntries
            .FirstOrDefaultAsync(memory => memory.UserProfileId == userId && memory.ProjectId == null && memory.Id == memoryId, cancellationToken);

    public Task<MemoryEntry?> GetProjectMemoryAsync(int userId, string projectId, int memoryId, CancellationToken cancellationToken = default)
        => context.MemoryEntries
            .FirstOrDefaultAsync(memory => memory.UserProfileId == userId && memory.ProjectId == projectId && memory.Id == memoryId, cancellationToken);

    public async Task<MemoryEntry> AddAsync(MemoryEntry memory, CancellationToken cancellationToken = default)
    {
        await context.MemoryEntries.AddAsync(memory, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return memory;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(MemoryEntry memory, CancellationToken cancellationToken = default)
    {
        context.MemoryEntries.Remove(memory);
        await context.SaveChangesAsync(cancellationToken);
    }
}
