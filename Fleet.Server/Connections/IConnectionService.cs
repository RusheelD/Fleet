using Fleet.Server.Models;

namespace Fleet.Server.Connections;

public interface IConnectionService
{
    Task<LinkedAccountDto> LinkGitHubAsync(int userId, string code, string redirectUri);
    Task UnlinkGitHubAsync(int userId);
    Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId);
    Task<IReadOnlyList<GitHubRepoDto>> GetGitHubRepositoriesAsync(int userId);
}
