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
}
