using Fleet.Server.Data.Entities;

namespace Fleet.Server.Memories;

public interface IMemoryRepository
{
    Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntry>> GetProjectMemoriesAsync(int userId, string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryEntry>> GetPromptMemoriesAsync(int userId, string? projectId, CancellationToken cancellationToken = default);
    Task<MemoryEntry?> GetUserMemoryAsync(int userId, int memoryId, CancellationToken cancellationToken = default);
    Task<MemoryEntry?> GetProjectMemoryAsync(int userId, string projectId, int memoryId, CancellationToken cancellationToken = default);
    Task<MemoryEntry> AddAsync(MemoryEntry memory, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(MemoryEntry memory, CancellationToken cancellationToken = default);
}
