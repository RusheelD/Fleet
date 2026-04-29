using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Auth;

public class AuthRepository(FleetDbContext context) : IAuthRepository
{
    public async Task<UserProfile?> GetByEntraObjectIdAsync(string entraObjectId)
    {
        return await context.UserProfiles
            .FirstOrDefaultAsync(u => u.EntraObjectId == entraObjectId);
    }

    public async Task<UserProfile?> GetByEmailAsync(string email)
    {
        return await context.UserProfiles
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<UserProfile?> GetByIdAsync(int id)
    {
        return await context.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<LoginIdentity?> GetLoginIdentityAsync(string provider, string providerUserId)
    {
        return await context.LoginIdentities
            .Include(i => i.UserProfile)
            .FirstOrDefaultAsync(i => i.Provider == provider && i.ProviderUserId == providerUserId);
    }

    public async Task<LoginIdentity?> GetLoginIdentityByIdAsync(int userId, int identityId)
    {
        return await context.LoginIdentities
            .FirstOrDefaultAsync(i => i.UserProfileId == userId && i.Id == identityId);
    }

    public async Task<IReadOnlyList<LoginIdentity>> GetLoginIdentitiesAsync(int userId)
    {
        return await context.LoginIdentities
            .Where(i => i.UserProfileId == userId)
            .OrderBy(i => i.Provider)
            .ThenBy(i => i.Email)
            .ThenBy(i => i.Id)
            .ToListAsync();
    }

    public async Task<int> CountLoginIdentitiesAsync(int userId)
    {
        return await context.LoginIdentities
            .CountAsync(i => i.UserProfileId == userId);
    }

    public async Task<UserProfile> CreateUserAsync(UserProfile user)
    {
        context.UserProfiles.Add(user);
        try
        {
            await context.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException)
        {
            // Detach the failed entity so it doesn't pollute subsequent
            // SaveChangesAsync calls within the same scoped DbContext.
            context.Entry(user).State = EntityState.Detached;
            throw;
        }
    }

    public async Task UpdateUserAsync(UserProfile user)
    {
        context.UserProfiles.Update(user);
        await context.SaveChangesAsync();
    }

    public async Task<LoginIdentity> CreateLoginIdentityAsync(LoginIdentity identity)
    {
        context.LoginIdentities.Add(identity);
        await context.SaveChangesAsync();
        return identity;
    }

    public async Task UpdateLoginIdentityAsync(LoginIdentity identity)
    {
        context.LoginIdentities.Update(identity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteLoginIdentityAsync(LoginIdentity identity)
    {
        context.LoginIdentities.Remove(identity);
        await context.SaveChangesAsync();
    }
}
