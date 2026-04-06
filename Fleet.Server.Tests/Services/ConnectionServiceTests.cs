using Fleet.Server.Connections;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ConnectionServiceTests
{
    private Mock<IConnectionRepository> _connectionRepo = null!;
    private Mock<IHttpClientFactory> _httpClientFactory = null!;
    private Mock<IConfiguration> _configuration = null!;
    private Mock<ILogger<ConnectionService>> _logger = null!;
    private ConnectionService _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _connectionRepo = new Mock<IConnectionRepository>();
        _httpClientFactory = new Mock<IHttpClientFactory>();
        _configuration = new Mock<IConfiguration>();
        _logger = new Mock<ILogger<ConnectionService>>();

        _configuration.Setup(c => c["GitHub:ClientId"]).Returns("test-client-id");
        _configuration.Setup(c => c["GitHub:ClientSecret"]).Returns("test-client-secret");

        _sut = new ConnectionService(
            _connectionRepo.Object,
            _httpClientFactory.Object,
            _configuration.Object,
            _logger.Object);
    }

    // ── GetConnectionsAsync ──────────────────────────────────

    [TestMethod]
    public async Task GetConnectionsAsync_DelegatesToRepo()
    {
        var connections = new List<LinkedAccountDto>
        {
            new(1, "GitHub", "octocat", "12345", DateTime.UtcNow)
        };
        _connectionRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(connections);

        var result = await _sut.GetConnectionsAsync(UserId);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("GitHub", result[0].Provider);
        Assert.AreEqual("octocat", result[0].ConnectedAs);
    }

    [TestMethod]
    public async Task GetConnectionsAsync_Empty_ReturnsEmpty()
    {
        _connectionRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync([]);

        var result = await _sut.GetConnectionsAsync(UserId);

        Assert.AreEqual(0, result.Count);
    }

    // ── UnlinkGitHubAsync ────────────────────────────────────

    [TestMethod]
    public async Task UnlinkGitHubAsync_ExistingAccount_Deletes()
    {
        var account = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            UserProfileId = UserId
        };
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub")).ReturnsAsync(account);

        await _sut.UnlinkGitHubAsync(UserId);

        _connectionRepo.Verify(r => r.DeleteAsync(account), Times.Once);
    }

    [TestMethod]
    public async Task UnlinkGitHubAsync_NoAccount_Throws()
    {
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync((LinkedAccount?)null);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.UnlinkGitHubAsync(UserId));
    }

    // ── LinkGitHubAsync ──────────────────────────────────────

    [TestMethod]
    public async Task LinkGitHubAsync_MissingClientId_Throws()
    {
        _configuration.Setup(c => c["GitHub:ClientId"]).Returns((string?)null);
        var sut = new ConnectionService(
            _connectionRepo.Object, _httpClientFactory.Object,
            _configuration.Object, _logger.Object);
        var state = await sut.CreateGitHubOAuthStateAsync(UserId);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => sut.LinkGitHubAsync(UserId, "code", "http://redirect", state.State));
    }

    [TestMethod]
    public async Task LinkGitHubAsync_MissingState_ThrowsUnauthorized()
    {
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            () => _sut.LinkGitHubAsync(UserId, "code", "http://redirect", ""));
    }

    [TestMethod]
    public async Task LinkGitHubAsync_MissingClientSecret_Throws()
    {
        _configuration.Setup(c => c["GitHub:ClientSecret"]).Returns((string?)null);
        var sut = new ConnectionService(
            _connectionRepo.Object, _httpClientFactory.Object,
            _configuration.Object, _logger.Object);
        var state = await sut.CreateGitHubOAuthStateAsync(UserId);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => sut.LinkGitHubAsync(UserId, "code", "http://redirect", state.State));
    }

    [TestMethod]
    public async Task LinkGitHubAsync_PersistsOAuthAccessTokenWithoutRefreshMetadata()
    {
        LinkedAccount? createdAccount = null;
        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([]);
        _connectionRepo.Setup(r => r.CreateAsync(It.IsAny<LinkedAccount>()))
            .Callback<LinkedAccount>(account => createdAccount = account)
            .ReturnsAsync((LinkedAccount account) => account);

        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.OK, """
                {
                  "access_token": "gho_access-123",
                  "refresh_token": "ghr_refresh-123",
                  "expires_in": 28800,
                  "refresh_token_expires_in": 15897600,
                  "token_type": "bearer",
                  "scope": "repo read:user user:email"
                }
                """),
            JsonResponse(HttpStatusCode.OK, """
                {
                  "id": 12345,
                  "login": "octocat"
                }
                """),
        ]);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, _) =>
            {
                if (responses.Count == 0)
                    throw new InvalidOperationException("No queued HTTP response.");

                return Task.FromResult(responses.Dequeue());
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var state = await _sut.CreateGitHubOAuthStateAsync(UserId);

        var result = await _sut.LinkGitHubAsync(UserId, "code-123", "http://redirect", state.State);

        Assert.AreEqual("octocat", result.ConnectedAs);
        Assert.IsNotNull(createdAccount);
        Assert.AreEqual("gho_access-123", createdAccount.AccessToken);
        Assert.AreEqual("12345", createdAccount.ExternalUserId);
    }

    // ── GetGitHubRepositoriesAsync ───────────────────────────

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_NoAccount_Throws()
    {
        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([]);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetGitHubRepositoriesAsync(UserId));
    }

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_NoToken_Throws()
    {
        var account = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = null,
            UserProfileId = UserId
        };
        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([account]);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetGitHubRepositoriesAsync(UserId));
    }

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_UsesStoredOAuthAccessToken_WhenLegacyRefreshMetadataExists()
    {
        var account = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "gho_access-token",
            UserProfileId = UserId,
            IsPrimary = true,
        };

        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([account]);

        string? repoListToken = null;

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                if ((request.RequestUri?.ToString() ?? string.Empty).Contains("/user/repos", StringComparison.Ordinal))
                    repoListToken = request.Headers.Authorization?.Parameter;

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
                    [
                      {
                        "full_name": "octocat/demo-repo",
                        "name": "demo-repo",
                        "owner": { "login": "octocat" },
                        "description": "Demo description",
                        "private": false,
                        "html_url": "https://github.com/octocat/demo-repo"
                      }
                    ]
                    """));
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var repos = await _sut.GetGitHubRepositoriesAsync(UserId);

        Assert.AreEqual(1, repos.Count);
        Assert.AreEqual("gho_access-token", repoListToken);
        Assert.AreEqual("gho_access-token", account.AccessToken);
        _connectionRepo.Verify(r => r.UpdateAsync(It.IsAny<LinkedAccount>()), Times.Never);
    }

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_SkipsUnauthorizedAccount_WhenAnotherAccountIsValid()
    {
        var invalidAccount = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "gho_invalid-token",
            UserProfileId = UserId,
            IsPrimary = true,
        };
        var validAccount = new LinkedAccount
        {
            Id = 2,
            Provider = "GitHub",
            ConnectedAs = "hubot",
            AccessToken = "gho_valid-token",
            UserProfileId = UserId,
        };

        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([invalidAccount, validAccount]);

        var seenTokens = new List<string?>();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.Unauthorized, """{ "message": "Bad credentials" }"""),
            JsonResponse(HttpStatusCode.OK, """
                [
                  {
                    "full_name": "hubot/demo-repo",
                    "name": "demo-repo",
                    "owner": { "login": "hubot" },
                    "description": "Demo description",
                    "private": false,
                    "html_url": "https://github.com/hubot/demo-repo"
                  }
                ]
                """),
        ]);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                if ((request.RequestUri?.ToString() ?? string.Empty).Contains("/user/repos", StringComparison.Ordinal))
                {
                    seenTokens.Add(request.Headers.Authorization?.Parameter);
                }

                if (responses.Count == 0)
                    throw new InvalidOperationException("No queued HTTP response.");

                return Task.FromResult(responses.Dequeue());
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var repos = await _sut.GetGitHubRepositoriesAsync(UserId);

        Assert.AreEqual(1, repos.Count);
        CollectionAssert.AreEqual(new[] { "gho_invalid-token", "gho_valid-token" }, seenTokens);
    }

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_AllAccountsUnauthorized_ThrowsRelinkMessage()
    {
        var firstAccount = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "gho_invalid-token-1",
            UserProfileId = UserId,
            IsPrimary = true,
        };
        var secondAccount = new LinkedAccount
        {
            Id = 2,
            Provider = "GitHub",
            ConnectedAs = "hubot",
            AccessToken = "gho_invalid-token-2",
            UserProfileId = UserId,
        };

        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([firstAccount, secondAccount]);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(HttpStatusCode.Unauthorized, """{ "message": "Bad credentials" }"""));

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetGitHubRepositoriesAsync(UserId));

        StringAssert.Contains(ex.Message, "Please re-link your GitHub account");
    }

    [TestMethod]
    public async Task ResolveGitHubAccessTokenForRepoAsync_UsesStoredOAuthAccessToken()
    {
        const string repoFullName = "octocat/demo-repo";
        var account = new LinkedAccount
        {
            Id = 1,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "gho_access-token",
            UserProfileId = UserId,
            IsPrimary = true,
        };

        _connectionRepo.Setup(r => r.GetByProviderAllAsync(UserId, "GitHub"))
            .ReturnsAsync([account]);

        string? repoProbeToken = null;

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                if ((request.RequestUri?.ToString() ?? string.Empty).Contains($"/repos/{repoFullName}", StringComparison.Ordinal))
                    repoProbeToken = request.Headers.Authorization?.Parameter;

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
                    {
                      "full_name": "octocat/demo-repo"
                    }
                    """));
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var token = await _sut.ResolveGitHubAccessTokenForRepoAsync(UserId, repoFullName);

        Assert.AreEqual("gho_access-token", token);
        Assert.AreEqual("gho_access-token", repoProbeToken);
        Assert.AreEqual("gho_access-token", account.AccessToken);
        _connectionRepo.Verify(r => r.UpdateAsync(It.IsAny<LinkedAccount>()), Times.Never);
    }

    [TestMethod]
    public async Task GetGitHubRepositoriesAsync_SpecificAccountUnauthorized_ThrowsRelinkMessage()
    {
        var account = new LinkedAccount
        {
            Id = 7,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "gho_invalid-token",
            UserProfileId = UserId,
            IsPrimary = true,
        };

        _connectionRepo.Setup(r => r.GetByIdAsync(UserId, account.Id))
            .ReturnsAsync(account);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(HttpStatusCode.Unauthorized, """{ "message": "Bad credentials" }"""));

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _sut.GetGitHubRepositoriesAsync(UserId, account.Id));

        StringAssert.Contains(ex.Message, "Please re-link your GitHub account");
    }

    [TestMethod]
    public async Task CreateGitHubRepositoryAsync_InitializesRepositoryWithReadmeUsingContentsApi()
    {
        var account = new LinkedAccount
        {
            Id = 7,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "token-123",
            UserProfileId = UserId,
            IsPrimary = true,
        };

        _connectionRepo.Setup(r => r.GetPrimaryByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync(account);
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync(account);

        var sentRequests = new List<(HttpMethod Method, string Url, string? Body)>();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.Created, """
                {
                  "full_name": "octocat/demo-repo",
                  "name": "demo-repo",
                  "owner": { "login": "octocat" },
                  "description": "Demo description",
                  "private": false,
                  "html_url": "https://github.com/octocat/demo-repo",
                  "default_branch": "main"
                }
                """),
            JsonResponse(HttpStatusCode.OK, """{ "ref": "refs/heads/main" }"""),
            JsonResponse(HttpStatusCode.OK, """{ "sha": "existing-readme-sha" }"""),
            JsonResponse(HttpStatusCode.OK, """{ "content": { "sha": "new-readme-sha" }, "commit": { "sha": "commit-sha" } }"""),
        ]);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync();
                sentRequests.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty, body));

                if (responses.Count == 0)
                    throw new InvalidOperationException("No queued HTTP response.");

                return responses.Dequeue();
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var result = await _sut.CreateGitHubRepositoryAsync(UserId,
            new CreateGitHubRepositoryRequest("demo-repo", "Demo description", false, null));

        Assert.AreEqual("octocat/demo-repo", result.FullName);
        Assert.AreEqual(4, sentRequests.Count);

        Assert.IsTrue(sentRequests[0].Url.EndsWith("/user/repos", StringComparison.Ordinal));
        using (var createPayload = JsonDocument.Parse(sentRequests[0].Body!))
        {
            Assert.AreEqual("demo-repo", createPayload.RootElement.GetProperty("name").GetString());
            Assert.AreEqual(false, createPayload.RootElement.GetProperty("auto_init").GetBoolean());
        }

        Assert.IsTrue(
            sentRequests[1].Url.EndsWith("/repos/octocat/demo-repo/git/ref/heads/main", StringComparison.Ordinal));
        Assert.IsTrue(
            sentRequests[2].Url.EndsWith("/repos/octocat/demo-repo/contents/README.md?ref=main", StringComparison.Ordinal));

        Assert.IsTrue(sentRequests[3].Url.EndsWith("/repos/octocat/demo-repo/contents/README.md", StringComparison.Ordinal));
        using (var putPayload = JsonDocument.Parse(sentRequests[3].Body!))
        {
            Assert.AreEqual("chore: initialize repository", putPayload.RootElement.GetProperty("message").GetString());
            Assert.AreEqual("main", putPayload.RootElement.GetProperty("branch").GetString());
            Assert.AreEqual("existing-readme-sha", putPayload.RootElement.GetProperty("sha").GetString());

            var encodedContent = putPayload.RootElement.GetProperty("content").GetString();
            var decodedContent = Encoding.UTF8.GetString(Convert.FromBase64String(encodedContent!));
            Assert.AreEqual("# demo-repo\n\nDemo description\n", decodedContent);
        }
    }

    [TestMethod]
    public async Task CreateGitHubRepositoryAsync_WithEmptyDescription_UsesTitleOnlyReadme()
    {
        var account = new LinkedAccount
        {
            Id = 7,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "token-123",
            UserProfileId = UserId,
            IsPrimary = true,
        };

        _connectionRepo.Setup(r => r.GetPrimaryByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync(account);
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync(account);

        string? putRequestBody = null;
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.Created, """
                {
                  "full_name": "octocat/empty-desc-repo",
                  "name": "empty-desc-repo",
                  "owner": { "login": "octocat" },
                  "description": null,
                  "private": false,
                  "html_url": "https://github.com/octocat/empty-desc-repo",
                  "default_branch": "main"
                }
                """),
            JsonResponse(HttpStatusCode.OK, """{ "ref": "refs/heads/main" }"""),
            JsonResponse(HttpStatusCode.OK, """{ "sha": "existing-readme-sha" }"""),
            JsonResponse(HttpStatusCode.OK, """{ "content": { "sha": "new-readme-sha" }, "commit": { "sha": "commit-sha" } }"""),
        ]);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                if (request.Method == HttpMethod.Put &&
                    (request.RequestUri?.ToString() ?? string.Empty)
                        .EndsWith("/repos/octocat/empty-desc-repo/contents/README.md", StringComparison.Ordinal))
                {
                    putRequestBody = request.Content is null
                        ? null
                        : await request.Content.ReadAsStringAsync();
                }

                if (responses.Count == 0)
                    throw new InvalidOperationException("No queued HTTP response.");

                return responses.Dequeue();
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        await _sut.CreateGitHubRepositoryAsync(UserId,
            new CreateGitHubRepositoryRequest("empty-desc-repo", null, false, null));

        Assert.IsNotNull(putRequestBody);
        using var putPayload = JsonDocument.Parse(putRequestBody);
        var encodedContent = putPayload.RootElement.GetProperty("content").GetString();
        var decodedContent = Encoding.UTF8.GetString(Convert.FromBase64String(encodedContent!));
        Assert.AreEqual("# empty-desc-repo\n", decodedContent);
    }

    [TestMethod]
    public async Task CreateGitHubRepositoryAsync_BootstrapsEmptyRepositoryWithInitialReadmeCommit()
    {
        var account = new LinkedAccount
        {
            Id = 7,
            Provider = "GitHub",
            ConnectedAs = "octocat",
            AccessToken = "token-123",
            UserProfileId = UserId,
            IsPrimary = true,
        };

        _connectionRepo.Setup(r => r.GetPrimaryByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync(account);
        _connectionRepo.Setup(r => r.GetByProviderAsync(UserId, "GitHub"))
            .ReturnsAsync(account);

        var sentRequests = new List<(HttpMethod Method, string Url, string? Body)>();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.Created, """
                {
                  "full_name": "octocat/brand-new-repo",
                  "name": "brand-new-repo",
                  "owner": { "login": "octocat" },
                  "description": "Fresh start",
                  "private": false,
                  "html_url": "https://github.com/octocat/brand-new-repo",
                  "default_branch": "main"
                }
                """),
            JsonResponse(HttpStatusCode.Conflict, """{ "message": "Git Repository is empty." }"""),
            JsonResponse(HttpStatusCode.Created, """{ "sha": "blob-sha" }"""),
            JsonResponse(HttpStatusCode.Created, """{ "sha": "tree-sha" }"""),
            JsonResponse(HttpStatusCode.Created, """{ "sha": "commit-sha" }"""),
            JsonResponse(HttpStatusCode.Created, """{ "ref": "refs/heads/main", "object": { "sha": "commit-sha" } }"""),
        ]);

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync();
                sentRequests.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty, body));

                if (responses.Count == 0)
                    throw new InvalidOperationException("No queued HTTP response.");

                return responses.Dequeue();
            });

        var httpClient = new HttpClient(handler.Object);
        _httpClientFactory.Setup(f => f.CreateClient("GitHub")).Returns(httpClient);

        var result = await _sut.CreateGitHubRepositoryAsync(UserId,
            new CreateGitHubRepositoryRequest("brand-new-repo", "Fresh start", false, null));

        Assert.AreEqual("octocat/brand-new-repo", result.FullName);
        Assert.AreEqual(6, sentRequests.Count);
        Assert.IsTrue(sentRequests[1].Url.EndsWith("/repos/octocat/brand-new-repo/git/ref/heads/main", StringComparison.Ordinal));
        Assert.IsTrue(sentRequests[2].Url.EndsWith("/repos/octocat/brand-new-repo/git/blobs", StringComparison.Ordinal));
        Assert.IsTrue(sentRequests[3].Url.EndsWith("/repos/octocat/brand-new-repo/git/trees", StringComparison.Ordinal));
        Assert.IsTrue(sentRequests[4].Url.EndsWith("/repos/octocat/brand-new-repo/git/commits", StringComparison.Ordinal));
        Assert.IsTrue(sentRequests[5].Url.EndsWith("/repos/octocat/brand-new-repo/git/refs", StringComparison.Ordinal));
        Assert.IsFalse(sentRequests.Any(request => request.Url.Contains("/contents/README.md", StringComparison.Ordinal)));

        using (var createPayload = JsonDocument.Parse(sentRequests[0].Body!))
        {
            Assert.AreEqual(false, createPayload.RootElement.GetProperty("auto_init").GetBoolean());
        }

        using (var blobPayload = JsonDocument.Parse(sentRequests[2].Body!))
        {
            Assert.AreEqual("utf-8", blobPayload.RootElement.GetProperty("encoding").GetString());
            Assert.AreEqual("# brand-new-repo\n\nFresh start\n", blobPayload.RootElement.GetProperty("content").GetString());
        }

        using (var refPayload = JsonDocument.Parse(sentRequests[5].Body!))
        {
            Assert.AreEqual("refs/heads/main", refPayload.RootElement.GetProperty("ref").GetString());
            Assert.AreEqual("commit-sha", refPayload.RootElement.GetProperty("sha").GetString());
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}
