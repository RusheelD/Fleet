using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Mcp;

public class McpServerRepository(FleetDbContext context) : IMcpServerRepository
{
    public async Task<IReadOnlyList<McpServerConnection>> GetAllAsync(int userId)
    {
        return await context.McpServerConnections
            .Where(server => server.UserProfileId == userId)
            .OrderBy(server => server.Name)
            .ThenBy(server => server.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<McpServerConnection>> GetEnabledAsync(int userId)
    {
        return await context.McpServerConnections
            .Where(server => server.UserProfileId == userId && server.Enabled)
            .OrderBy(server => server.Name)
            .ThenBy(server => server.Id)
            .ToListAsync();
    }

    public async Task<McpServerConnection?> GetByIdAsync(int userId, int id)
    {
        return await context.McpServerConnections
            .FirstOrDefaultAsync(server => server.UserProfileId == userId && server.Id == id);
    }

    public async Task<bool> NameExistsAsync(int userId, string name, int? excludingId = null)
    {
        var normalizedName = name.Trim();
        return await context.McpServerConnections.AnyAsync(server =>
            server.UserProfileId == userId &&
            server.Name == normalizedName &&
            (!excludingId.HasValue || server.Id != excludingId.Value));
    }

    public async Task<McpServerConnection> CreateAsync(McpServerConnection server)
    {
        context.McpServerConnections.Add(server);
        await context.SaveChangesAsync();
        return server;
    }

    public async Task UpdateAsync(McpServerConnection server)
    {
        context.McpServerConnections.Update(server);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(McpServerConnection server)
    {
        context.McpServerConnections.Remove(server);
        await context.SaveChangesAsync();
    }
}
