using Fleet.Server.Data.Entities;

namespace Fleet.Server.Auth;

public interface IAuthRepository
{
    Task<UserProfile?> GetByEntraObjectIdAsync(string entraObjectId);
    Task<UserProfile?> GetByIdAsync(int id);
    Task<UserProfile> CreateUserAsync(UserProfile user);
}
