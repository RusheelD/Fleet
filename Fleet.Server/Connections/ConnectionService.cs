using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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

        var externalUserId = gitHubUser.Id.ToString();
        var linkedAccounts = await connectionRepository.GetByProviderAllAsync(userId, "GitHub") ?? [];
        var existing = linkedAccounts.FirstOrDefault(a =>
            string.Equals(a.ExternalUserId, externalUserId, StringComparison.Ordinal));
        var hasPrimary = linkedAccounts.Any(a => a.IsPrimary);

        logger.ConnectionsLinkingGitHubAccount(userId, gitHubUser.Login.SanitizeForLogging(), gitHubUser.Id);

        if (existing is not null)
        {
            // Update the existing linked account with fresh token/profile
            existing.ConnectedAs = gitHubUser.Login;
            existing.AccessToken = _tokenProtector.Protect(tokenBody.AccessToken);
            existing.ExternalUserId = externalUserId;
            existing.ConnectedAt = DateTime.UtcNow;
            if (!hasPrimary)
                existing.IsPrimary = true;
            await connectionRepository.UpdateAsync(existing);

            return ToDto(existing);
        }

        // Store an additional linked account for this distinct external GitHub user.
        var linkedAccount = new LinkedAccount
        {
            Provider = "GitHub",
            ConnectedAs = gitHubUser.Login,
            AccessToken = _tokenProtector.Protect(tokenBody.AccessToken),
            ExternalUserId = externalUserId,
            ConnectedAt = DateTime.UtcNow,
            UserProfileId = userId,
            IsPrimary = linkedAccounts.Count == 0 || !hasPrimary,
        };

        await connectionRepository.CreateAsync(linkedAccount);

        return ToDto(linkedAccount);
    }

    public async Task<LinkedAccountDto> SetPrimaryGitHubAccountAsync(int userId, int accountId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub",
            ["AccountId"] = accountId
        });

        var linkedAccounts = await connectionRepository.GetByProviderAllAsync(userId, "GitHub") ?? [];
        if (linkedAccounts.Count == 0)
            throw new InvalidOperationException("No GitHub account is linked.");

        var target = linkedAccounts.FirstOrDefault(a => a.Id == accountId);
        if (target is null)
            throw new InvalidOperationException("No matching GitHub account is linked.");

        var hasUpdates = false;
        foreach (var account in linkedAccounts)
        {
            var shouldBePrimary = account.Id == target.Id;
            if (account.IsPrimary == shouldBePrimary)
                continue;

            account.IsPrimary = shouldBePrimary;
            await connectionRepository.UpdateAsync(account);
            hasUpdates = true;
        }

        if (!hasUpdates && !target.IsPrimary)
        {
            target.IsPrimary = true;
            await connectionRepository.UpdateAsync(target);
        }

        return ToDto(target);
    }

    public async Task UnlinkGitHubAsync(int userId, int? accountId = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub"
        });

        LinkedAccount? account = null;
        if (accountId is > 0)
        {
            var byId = await connectionRepository.GetByIdAsync(userId, accountId.Value);
            if (byId is not null && string.Equals(byId.Provider, "GitHub", StringComparison.OrdinalIgnoreCase))
            {
                account = byId;
            }
        }
        else
        {
            account = await connectionRepository.GetByProviderAsync(userId, "GitHub");
        }

        if (account is null)
            throw new InvalidOperationException("No matching GitHub account is linked.");

        logger.ConnectionsUnlinkingGitHubAccount(userId, (account.ConnectedAs ?? string.Empty).SanitizeForLogging());

        await connectionRepository.DeleteAsync(account);
        await EnsureSinglePrimaryGitHubAsync(userId);
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetConnectionsAsync(int userId)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId
        });

        return await connectionRepository.GetAllAsync(userId);
    }

    public async Task<string?> GetGitHubAccessTokenAsync(
        int userId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await connectionRepository.GetByIdAsync(userId, accountId);
        if (account is null || !string.Equals(account.Provider, "GitHub", StringComparison.OrdinalIgnoreCase))
            return null;

        return await GetValidGitHubAccessTokenAsync(account, cancellationToken);
    }

    public async Task<string?> GetPrimaryGitHubAccessTokenAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var account = await connectionRepository.GetPrimaryByProviderAsync(userId, "GitHub")
            ?? await connectionRepository.GetByProviderAsync(userId, "GitHub");
        if (account is null)
            return null;

        return await GetValidGitHubAccessTokenAsync(account, cancellationToken);
    }

    public async Task<string?> ResolveGitHubAccessTokenForRepoAsync(
        int userId,
        string repoFullName,
        CancellationToken cancellationToken = default)
    {
        var accounts = await connectionRepository.GetByProviderAllAsync(userId, "GitHub") ?? [];
        if (accounts.Count == 0)
            return null;

        var client = httpClientFactory.CreateClient("GitHub");
        foreach (var account in accounts)
        {
            var accessToken = await GetValidGitHubAccessTokenAsync(account, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
                continue;

            if (await RepoExistsForTokenAsync(client, accessToken, repoFullName, cancellationToken))
                return accessToken;
        }

        return null;
    }

    private static async Task<bool> RepoExistsForTokenAsync(
        HttpClient client,
        string accessToken,
        string repoFullName,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repoFullName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<GitHubRepoDto>> GetGitHubRepositoriesAsync(int userId, int? accountId = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub"
        });

        IReadOnlyList<LinkedAccount> accounts;
        if (accountId is > 0)
        {
            var specific = await connectionRepository.GetByIdAsync(userId, accountId.Value);
            if (specific is null || !string.Equals(specific.Provider, "GitHub", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("No matching GitHub account is linked.");
            }
            accounts = [specific];
        }
        else
        {
            accounts = await connectionRepository.GetByProviderAllAsync(userId, "GitHub") ?? [];
        }

        if (accounts.Count == 0)
            throw new InvalidOperationException("No GitHub account is linked.");

        var httpClient = httpClientFactory.CreateClient("GitHub");
        var reposByFullName = new Dictionary<string, GitHubRepoDto>(StringComparer.OrdinalIgnoreCase);
        var hasUsableToken = false;
        var hasAuthorizedRepositoryAccess = false;

        foreach (var account in accounts)
        {
            var accessToken = await GetValidGitHubAccessTokenAsync(account, CancellationToken.None);
            if (string.IsNullOrEmpty(accessToken))
            {
                if (accountId is > 0)
                    throw new InvalidOperationException("GitHub access token is missing. Please re-link your GitHub account.");
                continue;
            }

            hasUsableToken = true;
            var page = 1;
            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.github.com/user/repos?per_page=100&page={page}&sort=updated&direction=desc");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.UserAgent.ParseAdd("Fleet/1.0");

                var response = await httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (accountId is > 0)
                    {
                        throw new InvalidOperationException(
                            "GitHub connection is no longer valid. Please re-link your GitHub account.");
                    }

                    logger.LogWarning(
                        "Skipping invalid GitHub token for account {AccountId} ({ConnectedAs}) while listing repositories.",
                        account.Id,
                        account.ConnectedAs);
                    break;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var details = await GetGitHubErrorDetailsAsync(response);
                    throw new InvalidOperationException($"GitHub repository listing failed: {details}");
                }

                hasAuthorizedRepositoryAccess = true;

                var pageRepos = await response.Content.ReadFromJsonAsync<List<GitHubRepoListItem>>();
                if (pageRepos is null || pageRepos.Count == 0)
                    break;

                foreach (var repo in pageRepos)
                {
                    if (!reposByFullName.ContainsKey(repo.FullName))
                    {
                        reposByFullName[repo.FullName] = new GitHubRepoDto(
                            repo.FullName,
                            repo.Name,
                            repo.Owner?.Login ?? string.Empty,
                            repo.Description,
                            repo.Private,
                            repo.HtmlUrl,
                            account.Id,
                            account.ConnectedAs);
                    }
                }

                if (pageRepos.Count < 100)
                    break;

                page++;
            }
        }

        if (!hasUsableToken)
            throw new InvalidOperationException("GitHub access token is missing. Please re-link your GitHub account.");

        if (!hasAuthorizedRepositoryAccess)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        var repos = reposByFullName.Values
            .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        logger.ConnectionsFetchedGitHubRepositories(userId, repos.Count);
        return repos;
    }

    public async Task<GitHubRepoDto> CreateGitHubRepositoryAsync(int userId, CreateGitHubRepositoryRequest request)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = userId,
            ["Provider"] = "GitHub",
            ["AccountId"] = request.AccountId
        });

        var repositoryName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(repositoryName))
            throw new InvalidOperationException("Repository name is required.");

        var account = await ResolveGitHubAccountAsync(userId, request.AccountId);
        var accessToken = await GetValidGitHubAccessTokenAsync(account, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("GitHub access token is missing. Please re-link your GitHub account.");
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        var httpClient = httpClientFactory.CreateClient("GitHub");
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/user/repos");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
        createRequest.Content = JsonContent.Create(new GitHubCreateRepoRequest(
            repositoryName,
            normalizedDescription,
            request.Private,
            false));

        var createResponse = await httpClient.SendAsync(createRequest);
        if (createResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadFromJsonAsync<GitHubApiErrorResponse>();
            var errorMessage = error?.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(errorMessage))
                throw new InvalidOperationException($"GitHub repository creation failed: {errorMessage}");

            throw new InvalidOperationException(
                $"GitHub repository creation failed with status code {(int)createResponse.StatusCode} ({createResponse.StatusCode}).");
        }

        var createdRepo = await createResponse.Content.ReadFromJsonAsync<GitHubRepoListItem>();
        if (createdRepo is null || string.IsNullOrWhiteSpace(createdRepo.FullName))
            throw new InvalidOperationException("GitHub repository creation succeeded but returned an invalid response.");
        var defaultBranch = await ResolveRepositoryDefaultBranchAsync(
            httpClient,
            accessToken,
            createdRepo.FullName,
            createdRepo.DefaultBranch);
        var readmeContent = BuildReadmeContent(repositoryName, normalizedDescription);

        await EnsureRepositoryInitializedAsync(
            httpClient,
            accessToken,
            createdRepo.FullName,
            defaultBranch,
            readmeContent);

        return new GitHubRepoDto(
            createdRepo.FullName,
            createdRepo.Name,
            createdRepo.Owner?.Login ?? account.ConnectedAs ?? string.Empty,
            createdRepo.Description,
            createdRepo.Private,
            createdRepo.HtmlUrl,
            account.Id,
            account.ConnectedAs);
    }

    private async Task<string> ResolveRepositoryDefaultBranchAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string? hintedDefaultBranch)
    {
        if (!string.IsNullOrWhiteSpace(hintedDefaultBranch))
            return hintedDefaultBranch.Trim();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repositoryFullName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(response);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while resolving the default branch: {details}");
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (payload.RootElement.TryGetProperty("default_branch", out var defaultBranchProp))
        {
            var resolvedDefaultBranch = defaultBranchProp.GetString();
            if (!string.IsNullOrWhiteSpace(resolvedDefaultBranch))
                return resolvedDefaultBranch.Trim();
        }

        return "main";
    }

    private async Task EnsureRepositoryInitializedAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string defaultBranch,
        string readmeContent)
    {
        var branchExists = await BranchExistsAsync(
            httpClient,
            accessToken,
            repositoryFullName,
            defaultBranch);

        if (!branchExists)
        {
            await CreateInitialReadmeCommitAsync(
                httpClient,
                accessToken,
                repositoryFullName,
                defaultBranch,
                readmeContent);
            return;
        }

        await UpsertReadmeAsync(httpClient, accessToken, repositoryFullName, defaultBranch, readmeContent);
    }

    private async Task UpsertReadmeAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string defaultBranch,
        string readmeContent)
    {
        var readmeUrl = $"https://api.github.com/repos/{repositoryFullName}/contents/README.md";
        string? existingSha = null;

        using (var getRequest = new HttpRequestMessage(HttpMethod.Get, $"{readmeUrl}?ref={Uri.EscapeDataString(defaultBranch)}"))
        {
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            getRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");

            var getResponse = await httpClient.SendAsync(getRequest);
            if (getResponse.StatusCode == HttpStatusCode.NotFound)
            {
                existingSha = null;
            }
            else if (getResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    "GitHub connection is no longer valid. Please re-link your GitHub account.");
            }
            else if (!getResponse.IsSuccessStatusCode)
            {
                var details = await GetGitHubErrorDetailsAsync(getResponse);
                throw new InvalidOperationException(
                    $"GitHub repository initialization failed while reading README metadata: {details}");
            }
            else
            {
                var existingReadme = await getResponse.Content.ReadFromJsonAsync<GitHubContentFileResponse>();
                if (existingReadme is null || string.IsNullOrWhiteSpace(existingReadme.Sha))
                    throw new InvalidOperationException("GitHub repository initialization failed: README SHA was missing.");

                existingSha = existingReadme.Sha;
            }
        }

        var encodedReadme = Convert.ToBase64String(Encoding.UTF8.GetBytes(readmeContent));
        using var putRequest = new HttpRequestMessage(HttpMethod.Put, readmeUrl);
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        putRequest.Headers.UserAgent.ParseAdd("Fleet/1.0");
        putRequest.Content = JsonContent.Create(new GitHubUpsertFileRequest(
            "chore: initialize repository",
            encodedReadme,
            defaultBranch,
            existingSha));

        var putResponse = await httpClient.SendAsync(putRequest);
        if (putResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!putResponse.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(putResponse);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while writing README: {details}");
        }
    }

    private async Task<bool> BranchExistsAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string branchName)
    {
        var encodedBranch = Uri.EscapeDataString(branchName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{repositoryFullName}/git/ref/heads/{encodedBranch}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Conflict)
            return false;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(response);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while verifying branch '{branchName}': {details}");
        }

        return true;
    }

    private async Task CreateInitialReadmeCommitAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string defaultBranch,
        string readmeContent)
    {
        var blobSha = await CreateBlobAsync(httpClient, accessToken, repositoryFullName, readmeContent);
        var treeSha = await CreateTreeAsync(httpClient, accessToken, repositoryFullName, blobSha);
        var commitSha = await CreateCommitAsync(httpClient, accessToken, repositoryFullName, treeSha);
        await CreateRefAsync(httpClient, accessToken, repositoryFullName, defaultBranch, commitSha, readmeContent);
    }

    private async Task<string> CreateBlobAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string readmeContent)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{repositoryFullName}/git/blobs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");
        request.Content = JsonContent.Create(new GitHubCreateBlobRequest(readmeContent, "utf-8"));

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(response);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while creating the README blob: {details}");
        }

        var blob = await response.Content.ReadFromJsonAsync<GitHubBlobResponse>();
        if (blob is null || string.IsNullOrWhiteSpace(blob.Sha))
            throw new InvalidOperationException("GitHub repository initialization failed: README blob SHA was missing.");

        return blob.Sha;
    }

    private async Task<string> CreateTreeAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string blobSha)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{repositoryFullName}/git/trees");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");
        request.Content = JsonContent.Create(new GitHubCreateTreeRequest(
            [
                new GitHubTreeEntry("README.md", "100644", "blob", blobSha),
            ]));

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(response);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while creating the initial tree: {details}");
        }

        var tree = await response.Content.ReadFromJsonAsync<GitHubTreeResponse>();
        if (tree is null || string.IsNullOrWhiteSpace(tree.Sha))
            throw new InvalidOperationException("GitHub repository initialization failed: initial tree SHA was missing.");

        return tree.Sha;
    }

    private async Task<string> CreateCommitAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string treeSha)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{repositoryFullName}/git/commits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");
        request.Content = JsonContent.Create(new GitHubCreateCommitRequest(
            "chore: initialize repository",
            treeSha,
            []));

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(response);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while creating the initial commit: {details}");
        }

        var commit = await response.Content.ReadFromJsonAsync<GitHubCommitResponse>();
        if (commit is null || string.IsNullOrWhiteSpace(commit.Sha))
            throw new InvalidOperationException("GitHub repository initialization failed: initial commit SHA was missing.");

        return commit.Sha;
    }

    private async Task CreateRefAsync(
        HttpClient httpClient,
        string accessToken,
        string repositoryFullName,
        string defaultBranch,
        string commitSha,
        string readmeContent)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{repositoryFullName}/git/refs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Fleet/1.0");
        request.Content = JsonContent.Create(new GitHubCreateRefRequest($"refs/heads/{defaultBranch}", commitSha));

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "GitHub connection is no longer valid. Please re-link your GitHub account.");
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var branchExists = await BranchExistsAsync(
                httpClient,
                accessToken,
                repositoryFullName,
                defaultBranch);
            if (branchExists)
            {
                await UpsertReadmeAsync(httpClient, accessToken, repositoryFullName, defaultBranch, readmeContent);
                return;
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await GetGitHubErrorDetailsAsync(response);
            throw new InvalidOperationException(
                $"GitHub repository initialization failed while creating the default branch: {details}");
        }
    }

    private static async Task<string> GetGitHubErrorDetailsAsync(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<GitHubApiErrorResponse>();
        var errorMessage = error?.Message?.Trim();
        if (!string.IsNullOrWhiteSpace(errorMessage))
            return errorMessage;

        return $"status code {(int)response.StatusCode} ({response.StatusCode})";
    }

    private static string BuildReadmeContent(string repositoryName, string? description)
    {
        var heading = $"# {repositoryName}";
        if (string.IsNullOrWhiteSpace(description))
            return $"{heading}\n";

        return $"{heading}\n\n{description.Trim()}\n";
    }

    private async Task<LinkedAccount> ResolveGitHubAccountAsync(int userId, int? accountId)
    {
        if (accountId is > 0)
        {
            var specific = await connectionRepository.GetByIdAsync(userId, accountId.Value);
            if (specific is null || !string.Equals(specific.Provider, "GitHub", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("No matching GitHub account is linked.");

            return specific;
        }

        return await connectionRepository.GetPrimaryByProviderAsync(userId, "GitHub")
            ?? await connectionRepository.GetByProviderAsync(userId, "GitHub")
            ?? throw new InvalidOperationException("No GitHub account is linked.");
    }

    private Task<string?> GetValidGitHubAccessTokenAsync(
        LinkedAccount account,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(_tokenProtector.Unprotect(account.AccessToken));
    }
    private async Task EnsureSinglePrimaryGitHubAsync(int userId)
    {
        var accounts = await connectionRepository.GetByProviderAllAsync(userId, "GitHub") ?? [];
        if (accounts.Count == 0)
            return;

        var primaryAccount = accounts.FirstOrDefault(a => a.IsPrimary) ?? accounts[0];
        var primaryCount = accounts.Count(a => a.IsPrimary);
        if (primaryCount == 1 && primaryAccount.IsPrimary)
            return;

        foreach (var account in accounts)
        {
            var shouldBePrimary = account.Id == primaryAccount.Id;
            if (account.IsPrimary == shouldBePrimary)
                continue;

            account.IsPrimary = shouldBePrimary;
            await connectionRepository.UpdateAsync(account);
        }
    }

    private static LinkedAccountDto ToDto(LinkedAccount account) =>
        new(
            account.Id,
            account.Provider,
            account.ConnectedAs,
            account.ExternalUserId,
            account.ConnectedAt,
            account.IsPrimary);

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

        [JsonPropertyName("default_branch")]
        public string? DefaultBranch { get; set; }
    }

    private sealed class GitHubRepoOwner
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }

    private sealed record GitHubCreateRepoRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("private")] bool Private,
        [property: JsonPropertyName("auto_init")] bool AutoInit);

    private sealed record GitHubCreateBlobRequest(
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("encoding")] string Encoding);

    private sealed class GitHubBlobResponse
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }

    private sealed record GitHubTreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("sha")] string Sha);

    private sealed record GitHubCreateTreeRequest(
        [property: JsonPropertyName("tree")] IReadOnlyList<GitHubTreeEntry> Tree);

    private sealed class GitHubTreeResponse
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }

    private sealed record GitHubCreateCommitRequest(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("tree")] string Tree,
        [property: JsonPropertyName("parents")] IReadOnlyList<string> Parents);

    private sealed class GitHubCommitResponse
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }

    private sealed record GitHubCreateRefRequest(
        [property: JsonPropertyName("ref")] string Ref,
        [property: JsonPropertyName("sha")] string Sha);

    private sealed record GitHubUpsertFileRequest(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("branch")] string Branch,
        [property: JsonPropertyName("sha")] string? Sha);

    private sealed class GitHubContentFileResponse
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }

    private sealed class GitHubApiErrorResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
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
