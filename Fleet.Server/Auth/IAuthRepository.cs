using Fleet.Server.Data.Entities;

namespace Fleet.Server.Auth;

public interface IAuthRepository
{
    Task<UserProfile?> GetByEntraObjectIdAsync(string entraObjectId);
    Task<UserProfile?> GetByEmailAsync(string email);
    Task<UserProfile?> GetByIdAsync(int id);
    Task<LoginIdentity?> GetLoginIdentityAsync(string provider, string providerUserId);
    Task<LoginIdentity?> GetLoginIdentityByIdAsync(int userId, int identityId);
    Task<IReadOnlyList<LoginIdentity>> GetLoginIdentitiesAsync(int userId);
    Task<int> CountLoginIdentitiesAsync(int userId);
    Task<UserProfile> CreateUserAsync(UserProfile user);
    Task UpdateUserAsync(UserProfile user);
    Task<LoginIdentity> CreateLoginIdentityAsync(LoginIdentity identity);
    Task UpdateLoginIdentityAsync(LoginIdentity identity);
    Task DeleteLoginIdentityAsync(LoginIdentity identity);
}
