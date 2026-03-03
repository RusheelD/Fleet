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

    public async Task<UserProfile?> GetByIdAsync(int id)
    {
        return await context.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UserProfile> CreateUserAsync(UserProfile user)
    {
        context.UserProfiles.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
