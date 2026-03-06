using Fleet.Server.Models;

namespace Fleet.Server.Connections;

public interface IConnectionService
{
    Task<GitHubOAuthStateDto> CreateGitHubOAuthStateAsync(int userId);
    Task<LinkedAccountDto> LinkGitHubAsync(int userId, string code, string redirectUri, string state);
    Task UnlinkGitHubAsync(int userId);
    Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId);
    Task<IReadOnlyList<GitHubRepoDto>> GetGitHubRepositoriesAsync(int userId);
}
