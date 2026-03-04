using Fleet.Server.Agents;
using Fleet.Server.Auth;
using Fleet.Server.Connections;
using Fleet.Server.Copilot;
using Fleet.Server.Copilot.Tools;
using Fleet.Server.Data;
using Fleet.Server.Exceptions;
using Fleet.Server.GitHub;
using Fleet.Server.LLM;
using Fleet.Server.Logging;
using Fleet.Server.Projects;
using Fleet.Server.Search;
using Fleet.Server.Subscriptions;
using Fleet.Server.Users;
using Fleet.Server.WorkItems;
using Microsoft.Identity.Web;

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
builder.Services.AddTransient<OutboundHttpLoggingHandler>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ApiActionLoggingFilter>();
});

// Entra ID (Azure AD) Authentication
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

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
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ??
            string.Empty;
    }
});

// Named HttpClient for LLM with extended timeouts (tool-calling loops can take time)
builder.Services.AddHttpClient("LLM")
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(3))
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.Retry.MaxRetryAttempts = 2;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    });

builder.Services.AddSingleton<ILLMClient, ClaudeClient>();

// Chat tools (registered individually, collected by ChatToolRegistry)
builder.Services.AddScoped<IChatTool, GetProjectInfoTool>();
builder.Services.AddScoped<IChatTool, ListWorkItemsTool>();
builder.Services.AddScoped<IChatTool, CreateWorkItemTool>();
builder.Services.AddScoped<IChatTool, GetRepoTreeTool>();
builder.Services.AddScoped<IChatTool, ReadRepoFileTool>();
builder.Services.AddScoped<ChatToolRegistry>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IConnectionService, ConnectionService>();
builder.Services.AddScoped<IGitHubApiService, GitHubApiService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<IWorkItemLevelService, WorkItemLevelService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IUserService, UserService>();

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
app.UseCors();
app.UseAuthorization();

app.UseOutputCache();

app.MapControllers();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
