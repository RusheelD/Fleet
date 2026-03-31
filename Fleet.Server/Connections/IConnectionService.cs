using Fleet.Server.Models;

namespace Fleet.Server.Connections;

public interface IConnectionService
{
    Task<GitHubOAuthStateDto> CreateGitHubOAuthStateAsync(int userId);
    Task<LinkedAccountDto> LinkGitHubAsync(int userId, string code, string redirectUri, string state);
    Task<LinkedAccountDto> SetPrimaryGitHubAccountAsync(int userId, int accountId);
    Task UnlinkGitHubAsync(int userId, int? accountId = null);
    Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId);
    Task<IReadOnlyList<GitHubRepoDto>> GetGitHubRepositoriesAsync(int userId, int? accountId = null);
    Task<GitHubRepoDto> CreateGitHubRepositoryAsync(int userId, CreateGitHubRepositoryRequest request);
    Task<string?> GetGitHubAccessTokenAsync(int userId, int accountId, CancellationToken cancellationToken = default);
    Task<string?> GetPrimaryGitHubAccessTokenAsync(int userId, CancellationToken cancellationToken = default);
    Task<string?> ResolveGitHubAccessTokenForRepoAsync(
        int userId,
        string repoFullName,
        CancellationToken cancellationToken = default);
}
