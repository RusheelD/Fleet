using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Connections;

public class ConnectionRepository(FleetDbContext context) : IConnectionRepository
{
    public async Task<LinkedAccount?> GetByProviderAsync(int userId, string provider)
    {
        return await context.LinkedAccounts
            .Where(a => a.UserProfileId == userId && a.Provider == provider)
            .OrderByDescending(a => a.ConnectedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<LinkedAccount>> GetByProviderAllAsync(int userId, string provider)
    {
        return await context.LinkedAccounts
            .Where(a => a.UserProfileId == userId && a.Provider == provider)
            .OrderByDescending(a => a.ConnectedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public async Task<LinkedAccount?> GetByIdAsync(int userId, int accountId)
    {
        return await context.LinkedAccounts
            .FirstOrDefaultAsync(a => a.UserProfileId == userId && a.Id == accountId);
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetAllAsync(int userId)
    {
        return await context.LinkedAccounts
            .Where(a => a.UserProfileId == userId)
            .OrderByDescending(a => a.ConnectedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new LinkedAccountDto(a.Id, a.Provider, a.ConnectedAs, a.ExternalUserId, a.ConnectedAt))
            .ToListAsync();
    }

    public async Task<LinkedAccount> CreateAsync(LinkedAccount account)
    {
        context.LinkedAccounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task UpdateAsync(LinkedAccount account)
    {
        context.LinkedAccounts.Update(account);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(LinkedAccount account)
    {
        context.LinkedAccounts.Remove(account);
        await context.SaveChangesAsync();
    }
}
