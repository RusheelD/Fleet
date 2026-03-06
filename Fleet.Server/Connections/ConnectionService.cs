using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Fleet.Server.Data.Entities;
using Fleet.Server.Logging;
using Fleet.Server.Models;

namespace Fleet.Server.Connections;

public class ConnectionService(
    IConnectionRepository connectionRepository,
    IGitHubTokenProtector? tokenProtector,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IDataProtectionProvider? dataProtectionProvider,
    ILogger<ConnectionService> logger) : IConnectionService
{
    private static readonly TimeSpan OAuthStateLifetime = TimeSpan.FromMinutes(10);
    private readonly IGitHubTokenProtector _tokenProtector = tokenProtector ?? new NoOpTokenProtector();
    private readonly IDataProtector _stateProtector =
        (dataProtectionProvider ?? DataProtectionProvider.Create("Fleet.ConnectionService"))
            .CreateProtector("Fleet.GitHub.OAuthState.v1");

    // Backward-compatible constructor for existing tests/callers.
    public ConnectionService(
        IConnectionRepository connectionRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ConnectionService> logger)
        : this(connectionRepository, null, httpClientFactory, configuration, null, logger)
    {
    }

    public Task<GitHubOAuthStateDto> CreateGitHubOAuthStateAsync(int userId)
    {
        var payload = new OAuthStatePayload(
            userId,
            DateTimeOffset.UtcNow.Add(OAuthStateLifetime).ToUnixTimeSeconds(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

        var json = JsonSerializer.Serialize(payload);
        var state = _stateProtector.Protect(json);
        return Task.FromResult(new GitHubOAuthStateDto(state));
    }

    public async Task<LinkedAccountDto> LinkGitHubAsync(int userId, string code, string redirectUri, string state)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub"
        });

        if (!ValidateState(userId, state))
            throw new UnauthorizedAccessException("Invalid or expired GitHub OAuth state.");

        var sanitizedRedirect = LogSanitizer.SanitizeUrl(redirectUri);
        logger.ConnectionsLinkGitHubRequested(userId, sanitizedRedirect.SanitizeForLogging());

        // Check if already linked
        var existing = await connectionRepository.GetByProviderAsync(userId, "GitHub");

        // Exchange authorization code for access token
        var clientId = configuration["GitHub:ClientId"]
            ?? throw new InvalidOperationException("GitHub:ClientId is not configured.");
        var clientSecret = configuration["GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("GitHub:ClientSecret is not configured.");

        var httpClient = httpClientFactory.CreateClient("GitHub");

        // GitHub OAuth token endpoint expects form-encoded data, not JSON
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
            { "redirect_uri", redirectUri },
        });

        var tokenResponse = await httpClient.PostAsync("https://github.com/login/oauth/access_token", tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<GitHubTokenResponse>();
        if (tokenBody is null || string.IsNullOrEmpty(tokenBody.AccessToken))
        {
            logger.ConnectionsGitHubTokenExchangeFailed((tokenBody?.Error ?? string.Empty).SanitizeForLogging());
            throw new InvalidOperationException(
                tokenBody?.ErrorDescription ?? "Failed to exchange GitHub authorization code for access token.");
        }

        // Fetch GitHub user profile
        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody.AccessToken);
        userRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var userResponse = await httpClient.SendAsync(userRequest);
        userResponse.EnsureSuccessStatusCode();

        var gitHubUser = await userResponse.Content.ReadFromJsonAsync<GitHubUserResponse>();
        if (gitHubUser is null)
            throw new InvalidOperationException("Failed to retrieve GitHub user profile.");

        logger.ConnectionsLinkingGitHubAccount(userId, gitHubUser.Login.SanitizeForLogging(), gitHubUser.Id);

        if (existing is not null)
        {
            // Update the existing linked account with fresh token/profile
            existing.ConnectedAs = gitHubUser.Login;
            existing.AccessToken = _tokenProtector.Protect(tokenBody.AccessToken);
            existing.ExternalUserId = gitHubUser.Id.ToString();
            existing.ConnectedAt = DateTime.UtcNow;
            await connectionRepository.UpdateAsync(existing);

            return new LinkedAccountDto("GitHub", gitHubUser.Login, gitHubUser.Id.ToString(), existing.ConnectedAt);
        }

        // Store the linked account
        var linkedAccount = new LinkedAccount
        {
            Provider = "GitHub",
            ConnectedAs = gitHubUser.Login,
            AccessToken = _tokenProtector.Protect(tokenBody.AccessToken),
            ExternalUserId = gitHubUser.Id.ToString(),
            ConnectedAt = DateTime.UtcNow,
            UserProfileId = userId,
        };

        await connectionRepository.CreateAsync(linkedAccount);

        return new LinkedAccountDto("GitHub", gitHubUser.Login, gitHubUser.Id.ToString(), linkedAccount.ConnectedAt);
    }

    public async Task UnlinkGitHubAsync(int userId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub"
        });

        var account = await connectionRepository.GetByProviderAsync(userId, "GitHub")
            ?? throw new InvalidOperationException("No GitHub account is linked.");

        logger.ConnectionsUnlinkingGitHubAccount(userId, (account.ConnectedAs ?? string.Empty).SanitizeForLogging());

        await connectionRepository.DeleteAsync(account);
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId
        });

        return await connectionRepository.GetAllAsync(userId);
    }

    public async Task<IReadOnlyList<GitHubRepoDto>> GetGitHubRepositoriesAsync(int userId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub"
        });

        var account = await connectionRepository.GetByProviderAsync(userId, "GitHub")
            ?? throw new InvalidOperationException("No GitHub account is linked.");

        var accessToken = _tokenProtector.Unprotect(account.AccessToken);
        if (string.IsNullOrEmpty(accessToken))
            throw new InvalidOperationException("GitHub access token is missing.");

        var httpClient = httpClientFactory.CreateClient("GitHub");
        var repos = new List<GitHubRepoDto>();
        var page = 1;

        // Paginate through all repos (GitHub returns max 100 per page)
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/user/repos?per_page=100&page={page}&sort=updated&direction=desc");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("Fleet/1.0");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var pageRepos = await response.Content.ReadFromJsonAsync<List<GitHubRepoListItem>>();
            if (pageRepos is null || pageRepos.Count == 0)
                break;

            repos.AddRange(pageRepos.Select(r => new GitHubRepoDto(
                r.FullName, r.Name, r.Owner?.Login ?? "", r.Description, r.Private, r.HtmlUrl)));

            if (pageRepos.Count < 100)
                break;

            page++;
        }

        logger.ConnectionsFetchedGitHubRepositories(userId, repos.Count);
        return repos;
    }

    // ── GitHub API response models ─────────────────────────────

    private sealed class GitHubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private sealed class GitHubUserResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
    }

    private sealed class GitHubRepoListItem
    {
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public GitHubRepoOwner? Owner { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
    }

    private sealed class GitHubRepoOwner
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }

    private sealed class NoOpTokenProtector : IGitHubTokenProtector
    {
        public string Protect(string token) => token;
        public string? Unprotect(string? protectedToken) => protectedToken;
    }

    private bool ValidateState(int userId, string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return false;

        try
        {
            var json = _stateProtector.Unprotect(state);
            var payload = JsonSerializer.Deserialize<OAuthStatePayload>(json);
            if (payload is null)
                return false;

            if (payload.UserId != userId)
                return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload.ExpiresAtUnix < now)
                return false;

            return !string.IsNullOrWhiteSpace(payload.Nonce);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record OAuthStatePayload(int UserId, long ExpiresAtUnix, string Nonce);
}
