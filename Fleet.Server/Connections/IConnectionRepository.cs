using Fleet.Server.Data.Entities;
using Fleet.Server.Models;

namespace Fleet.Server.Connections;

public interface IConnectionRepository
{
    Task<LinkedAccount?> GetByProviderAsync(int userId, string provider);
    Task<LinkedAccount?> GetPrimaryByProviderAsync(int userId, string provider);
    Task<IReadOnlyList<LinkedAccount>> GetByProviderAllAsync(int userId, string provider);
    Task<LinkedAccount?> GetByIdAsync(int userId, int accountId);
    Task<IReadOnlyList<LinkedAccountDto>> GetAllAsync(int userId);
    Task<LinkedAccount> CreateAsync(LinkedAccount account);
    Task UpdateAsync(LinkedAccount account);
    Task DeleteAsync(LinkedAccount account);
}
