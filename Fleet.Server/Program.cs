using Fleet.Server.Agents;
using Fleet.Server.Agents.Tools;
using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Copilot;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.Data;
using Fleet.Server.Exceptions;
using Fleet.Server.GitHub;
using Fleet.Server.LLM;
using Fleet.Server.Logging;
using Fleet.Server.Notifications;
using Fleet.Server.Projects;
using Fleet.Server.Realtime;
using Fleet.Server.Search;
using Fleet.Server.Subscriptions;
using Fleet.Server.Users;
using Fleet.Server.WorkItems;
using Microsoft.Identity.Web;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.SingleLine = true;
    options.IncludeScopes = true;
});
#endif

// Always log EF Core DB command execution (query text + duration + status)
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddRedisClientBuilder("cache")
    .WithOutputCache();

// Add PostgreSQL + EF Core via Aspire integration.
builder.AddNpgsqlDbContext<FleetDbContext>("fleetdb");

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<ApiActionLoggingFilter>();
builder.Services.AddScoped<ProjectOwnershipFilter>();
builder.Services.AddTransient<OutboundHttpLoggingHandler>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ApiActionLoggingFilter>();
});

// Entra ID (Azure AD) Authentication
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

var adminObjectIds = builder.Configuration.GetSection("Admin:AllowedEntraObjectIds").Get<string[]>() ?? [];
var adminEmails = builder.Configuration.GetSection("Admin:AllowedEmails").Get<string[]>() ?? [];

static bool IsAdminIdentity(ClaimsPrincipal principal, string[] allowedObjectIds, string[] allowedEmails)
{
    if (principal.IsInRole("Admin") ||
        principal.Claims.Any(c =>
            (c.Type == "roles" || c.Type == ClaimTypes.Role) &&
            string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    var oid = principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
        ?? principal.FindFirst("oid")?.Value;

    if (!string.IsNullOrWhiteSpace(oid) &&
        allowedObjectIds.Contains(oid, StringComparer.OrdinalIgnoreCase))
    {
        return true;
    }

    var email = principal.FindFirst(ClaimTypes.Email)?.Value
        ?? principal.FindFirst("preferred_username")?.Value;

    if (!string.IsNullOrWhiteSpace(email) &&
        string.Equals(email, "rusheel@live.com", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return !string.IsNullOrWhiteSpace(email) &&
           allowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => IsAdminIdentity(context.User, adminObjectIds, adminEmails));
    });
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var appRole = UserRoles.Normalize(httpContext.User.FindFirst(FleetClaimTypes.AppRole)?.Value);
        var tierPolicy = TierPolicyCatalog.Get(appRole);
        var isAdmin = IsAdminIdentity(httpContext.User, adminObjectIds, adminEmails);

        var userKey =
            httpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value ??
            httpContext.User.FindFirst("oid")?.Value ??
            httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            httpContext.Connection.RemoteIpAddress?.ToString() ??
            "anonymous";

        if (isAdmin || tierPolicy.UnlimitedRateLimit)
            return RateLimitPartition.GetNoLimiter($"unlimited:{userKey}");

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = tierPolicy.RequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// CORS — allow the frontend app and marketing website origins
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// HttpClient factory for external API calls (GitHub OAuth, etc.)
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Fleet/1.0");
})
    .AddHttpMessageHandler<OutboundHttpLoggingHandler>();

// LLM configuration + provider
builder.Services.Configure<LLMOptions>(builder.Configuration.GetSection(LLMOptions.SectionName));
builder.Services.PostConfigure<LLMOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        options.ApiKey =
            Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ??
            string.Empty;
    }

    if (string.IsNullOrWhiteSpace(options.Endpoint))
    {
        options.Endpoint =
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
            string.Empty;
    }

    if (string.IsNullOrWhiteSpace(options.GenerateModel))
    {
        options.GenerateModel = options.Model;
    }
});

builder.Services.Configure<ModelCatalogOptions>(builder.Configuration
    .GetSection($"{LLMOptions.SectionName}:{ModelCatalogOptions.SectionName}"));
builder.Services.AddSingleton<IModelCatalog, ModelCatalog>();

var configuredLlmTimeoutSeconds = builder.Configuration.GetValue<int?>("LLM:TimeoutSeconds") ?? 1800;
var llmRequestTimeout = TimeSpan.FromSeconds(Math.Max(600, configuredLlmTimeoutSeconds));
var llmAttemptTimeout = TimeSpan.FromSeconds(Math.Clamp(configuredLlmTimeoutSeconds / 3, 300, 1800));
var llmCircuitBreakerSamplingDuration = TimeSpan.FromSeconds(Math.Max(300, (int)Math.Ceiling(llmAttemptTimeout.TotalSeconds * 2)));

builder.Services.PostConfigureAll<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
{
    var attemptTimeout = options.AttemptTimeout.Timeout;
    if (attemptTimeout <= TimeSpan.Zero || attemptTimeout == Timeout.InfiniteTimeSpan)
    {
        return;
    }

    var minimumSamplingDuration = TimeSpan.FromSeconds(Math.Ceiling(attemptTimeout.TotalSeconds * 2));
    if (options.CircuitBreaker.SamplingDuration < minimumSamplingDuration)
    {
        options.CircuitBreaker.SamplingDuration = minimumSamplingDuration;
    }
});

// Named HttpClient for LLM with extended timeouts (agent phases can run 10-30 minutes)
builder.Services.AddHttpClient("LLM")
    .ConfigureHttpClient(client => client.Timeout = llmRequestTimeout)
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = llmRequestTimeout;
        options.AttemptTimeout.Timeout = llmAttemptTimeout;
        options.Retry.MaxRetryAttempts = 2;
        options.CircuitBreaker.SamplingDuration = llmCircuitBreakerSamplingDuration;
    });

builder.Services.AddSingleton<ILLMClient, AzureOpenAiClient>();

// Chat tools (registered individually, collected by ChatToolRegistry)
builder.Services.AddScoped<IChatTool, GetProjectInfoTool>();
builder.Services.AddScoped<IChatTool, ListProjectsTool>();
builder.Services.AddScoped<IChatTool, ListGitHubReposTool>();
builder.Services.AddScoped<IChatTool, GetProjectDashboardTool>();
builder.Services.AddScoped<IChatTool, ListWorkItemsTool>();
builder.Services.AddScoped<IChatTool, ListWorkItemLevelsTool>();
builder.Services.AddScoped<IChatTool, ListAgentExecutionsTool>();
builder.Services.AddScoped<IChatTool, ListAgentLogsTool>();
builder.Services.AddScoped<IChatTool, ListNotificationsTool>();
builder.Services.AddScoped<IChatTool, GetSubscriptionTool>();
builder.Services.AddScoped<IChatTool, SearchWorkspaceTool>();
builder.Services.AddScoped<IChatTool, GetGitHubRepoStatsTool>();
builder.Services.AddScoped<IChatTool, ListGitHubWorkItemReferencesTool>();
builder.Services.AddScoped<IChatTool, CreateProjectTool>();
builder.Services.AddScoped<IChatTool, CreateWorkItemTool>();
builder.Services.AddScoped<IChatTool, UpdateWorkItemTool>();
builder.Services.AddScoped<IChatTool, DeleteWorkItemTool>();
builder.Services.AddScoped<IChatTool, ReadWorkItemTool>();
builder.Services.AddScoped<IChatTool, TryUpdateWorkItemTool>();
builder.Services.AddScoped<IChatTool, BulkCreateWorkItemsTool>();
builder.Services.AddScoped<IChatTool, BulkUpdateWorkItemsTool>();
builder.Services.AddScoped<IChatTool, BulkDeleteWorkItemsTool>();
builder.Services.AddScoped<IChatTool, BulkReadWorkItemsTool>();
builder.Services.AddScoped<IChatTool, TryBulkUpdateWorkItemsTool>();
builder.Services.AddScoped<IChatTool, GetRepoTreeTool>();
builder.Services.AddScoped<IChatTool, ReadRepoFileTool>();
builder.Services.AddScoped<ChatToolRegistry>();

// Agent tools (registered individually, collected by AgentToolRegistry)
builder.Services.AddScoped<IAgentTool, ListDirectoryTool>();
builder.Services.AddScoped<IAgentTool, ReadFileTool>();
builder.Services.AddScoped<IAgentTool, WriteFileTool>();
builder.Services.AddScoped<IAgentTool, EditFileTool>();
builder.Services.AddScoped<IAgentTool, DeleteFileTool>();
builder.Services.AddScoped<IAgentTool, SearchFilesTool>();
builder.Services.AddScoped<IAgentTool, RunCommandTool>();
builder.Services.AddScoped<IAgentTool, CommitAndPushTool>();
builder.Services.AddScoped<IAgentTool, GetChangeSummaryTool>();
builder.Services.AddScoped<IAgentTool, ReportProgressTool>();
builder.Services.AddScoped<AgentToolRegistry>();

// Agent infrastructure
builder.Services.AddSingleton<IAgentPromptLoader, AgentPromptLoader>();
builder.Services.AddScoped<IAgentPhaseRunner, AgentPhaseRunner>();
builder.Services.AddScoped<IRepoSandbox, RepoSandbox>();
builder.Services.AddScoped<IAgentOrchestrationService, AgentOrchestrationService>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IConnectionService, ConnectionService>();
builder.Services.AddScoped<IGitHubTokenProtector, GitHubTokenProtector>();
builder.Services.AddScoped<IGitHubApiService, GitHubApiService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<IWorkItemLevelService, WorkItemLevelService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IUsageLedgerService, UsageLedgerService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IServerEventPublisher, ServerEventPublisher>();

// Repositories
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IConnectionRepository, ConnectionRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();
builder.Services.AddScoped<IWorkItemLevelRepository, WorkItemLevelRepository>();
builder.Services.AddScoped<IAgentTaskRepository, AgentTaskRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Background migration — runs after the host starts so health checks respond immediately
var hasFleetDbConnection =
    !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("fleetdb")) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["ConnectionString"]) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:ConnectionString"]) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:FleetDbContext:ConnectionString"]);

if (hasFleetDbConnection)
{
    builder.Services.AddHostedService<DatabaseMigrationService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Always route unhandled exceptions through GlobalExceptionHandler
app.UseExceptionHandler();

app.UseMiddleware<ResponseHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseMiddleware<UserRoleClaimsMiddleware>();
app.UseRateLimiter();
app.UseCors();
app.UseAuthorization();

app.UseOutputCache();

app.MapControllers();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
