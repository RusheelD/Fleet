using Fleet.Server.Data.Entities;

namespace Fleet.Server.Mcp;

public interface IMcpServerRepository
{
    Task<IReadOnlyList<McpServerConnection>> GetAllAsync(int userId);
    Task<IReadOnlyList<McpServerConnection>> GetEnabledAsync(int userId);
    Task<McpServerConnection?> GetByIdAsync(int userId, int id);
    Task<bool> NameExistsAsync(int userId, string name, int? excludingId = null);
    Task<McpServerConnection> CreateAsync(McpServerConnection server);
    Task UpdateAsync(McpServerConnection server);
    Task DeleteAsync(McpServerConnection server);
}
