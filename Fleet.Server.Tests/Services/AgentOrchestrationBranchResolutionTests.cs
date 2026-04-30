using System.Net;
using Fleet.Server.Agents;
using Fleet.Server.Connections;
using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.LLM;
using Fleet.Server.Notifications;
using Fleet.Server.Realtime;
using Fleet.Server.Subscriptions;
using Fleet.Server.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentOrchestrationBranchResolutionTests
{
    [TestMethod]
    public async Task ResolvePullRequestTargetBranchAsync_CreatesMissingRequestedBranchFromDefaultBranch()
    {
        var handler = new RecordingGitHubHandler(
            (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound),
            (_, _) => JsonResponse(HttpStatusCode.OK, """{"default_branch":"main"}"""),
            (_, _) => JsonResponse(HttpStatusCode.OK, """{"object":{"sha":"abc123"}}"""),
            (_, _) => JsonResponse(HttpStatusCode.Created, """{"ref":"refs/heads/feature/chat"}"""));
        var sut = CreateService(handler);

        var branch = await InvokeResolvePullRequestTargetBranchAsync(sut, "feature/chat");

        Assert.AreEqual("feature/chat", branch);
        Assert.IsTrue(handler.Requests.Any(request =>
            request.Method == HttpMethod.Post &&
            request.Url.EndsWith("/repos/owner/repo/git/refs", StringComparison.Ordinal)));
        var createRequest = handler.Requests.Single(request => request.Method == HttpMethod.Post);
        StringAssert.Contains(createRequest.Body ?? string.Empty, "\"ref\":\"refs/heads/feature/chat\"");
        StringAssert.Contains(createRequest.Body ?? string.Empty, "\"sha\":\"abc123\"");
    }

    [TestMethod]
    public async Task ResolvePullRequestTargetBranchAsync_TreatsCreateConflictAsSuccessWhenBranchNowExists()
    {
        var handler = new RecordingGitHubHandler(
            (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound),
            (_, _) => JsonResponse(HttpStatusCode.OK, """{"default_branch":"main"}"""),
            (_, _) => JsonResponse(HttpStatusCode.OK, """{"object":{"sha":"abc123"}}"""),
            (_, _) => JsonResponse(HttpStatusCode.UnprocessableEntity, """{"message":"Reference already exists"}"""),
            (_, _) => JsonResponse(HttpStatusCode.OK, """{"ref":"refs/heads/feature/chat"}"""));
        var sut = CreateService(handler);

        var branch = await InvokeResolvePullRequestTargetBranchAsync(sut, "feature/chat");

        Assert.AreEqual("feature/chat", branch);
    }

    [TestMethod]
    public async Task ResolvePullRequestTargetBranchAsync_UsesDefaultBranchWhenNoBranchRequested()
    {
        var handler = new RecordingGitHubHandler(
            (_, _) => JsonResponse(HttpStatusCode.OK, """{"default_branch":"develop"}"""));
        var sut = CreateService(handler);

        var branch = await InvokeResolvePullRequestTargetBranchAsync(sut, null);

        Assert.AreEqual("develop", branch);
        Assert.AreEqual(1, handler.Requests.Count);
        Assert.IsFalse(handler.Requests.Any(request => request.Method == HttpMethod.Post));
    }

    private static async Task<string> InvokeResolvePullRequestTargetBranchAsync(
        AgentOrchestrationService service,
        string? requestedBranch)
    {
        var method = typeof(AgentOrchestrationService).GetMethod(
            "ResolvePullRequestTargetBranchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var task = (Task<string>)method.Invoke(
            service,
            new object?[] { "token", "owner/repo", requestedBranch, CancellationToken.None })!;

        return await task;
    }

    private static AgentOrchestrationService CreateService(HttpMessageHandler handler)
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new FleetDbContext(options);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient("GitHub"))
            .Returns(new HttpClient(handler));

        return new AgentOrchestrationService(
            db,
            Mock.Of<IAgentTaskRepository>(),
            Mock.Of<IConnectionService>(),
            Mock.Of<IWorkItemRepository>(),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILLMClient>(),
            httpClientFactory.Object,
            Mock.Of<ILogger<AgentOrchestrationService>>(),
            Mock.Of<IModelCatalog>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IServerEventPublisher>(),
            new AgentCallCapacityManager(Options.Create(new LLMOptions { MaxConcurrentAgentCalls = 8 })),
            Mock.Of<IUsageLedgerService>());
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingGitHubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, string?, HttpResponseMessage>> _responses;

        public RecordingGitHubHandler(params Func<HttpRequestMessage, string?, HttpResponseMessage>[] responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, string?, HttpResponseMessage>>(responses);
        }

        public List<RecordedGitHubRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedGitHubRequest(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                body));

            return _responses.Count > 0
                ? _responses.Dequeue()(request, body)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed record RecordedGitHubRequest(HttpMethod Method, string Url, string? Body);
}
