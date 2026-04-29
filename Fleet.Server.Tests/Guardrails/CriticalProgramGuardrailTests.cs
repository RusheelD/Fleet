namespace Fleet.Server.Tests.Guardrails;

[TestClass]
public class CriticalProgramGuardrailTests
{
    [TestMethod]
    public void CriticalServicesAndRepositoriesRemainRegistered()
    {
        var program = ReadProgramSource();
        var requiredRegistrations = new[]
        {
            "AddScoped<IAuthService, AuthService>",
            "AddScoped<IConnectionService, ConnectionService>",
            "AddScoped<IMcpServerService, McpServerService>",
            "AddScoped<IMemoryService, MemoryService>",
            "AddScoped<ISkillService, SkillService>",
            "AddScoped<IProjectService, ProjectService>",
            "AddScoped<IWorkItemService, WorkItemService>",
            "AddScoped<IWorkItemLevelService, WorkItemLevelService>",
            "AddScoped<IAgentService, AgentService>",
            "AddScoped<IAgentOrchestrationService, AgentOrchestrationService>",
            "AddScoped<IAgentExecutionDispatcher, AgentExecutionDispatcher>",
            "AddScoped<IAgentAutoExecutionDispatcher, AgentAutoExecutionDispatcher>",
            "AddScoped<IDynamicIterationDispatchService, DynamicIterationDispatchService>",
            "AddScoped<ChatService>",
            "AddScoped<IChatService>",
            "AddScoped<IUsageLedgerService, UsageLedgerService>",
            "AddScoped<ISubscriptionService, SubscriptionService>",
            "AddScoped<IUserService, UserService>",
            "AddScoped<INotificationService, NotificationService>",
            "AddScoped<ProjectOwnershipFilter>",
            "AddScoped<IAuthRepository, AuthRepository>",
            "AddScoped<IProjectRepository, ProjectRepository>",
            "AddScoped<IWorkItemRepository, WorkItemRepository>",
            "AddScoped<IChatSessionRepository, ChatSessionRepository>",
            "AddSingleton<IServerEventPublisher, ServerEventPublisher>",
        };

        var missingRegistrations = requiredRegistrations
            .Where(registration => !program.Contains(registration, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            missingRegistrations.Length,
            "Critical Fleet services/repositories must stay registered in Program.cs. Missing: " +
            string.Join(", ", missingRegistrations));
    }

    [TestMethod]
    public void SecurityAndRoutingMiddlewareStayInSafeOrder()
    {
        var program = ReadProgramSource();

        AssertInOrder(program,
            "app.UseExceptionHandler();",
            "app.UseAuthentication();",
            "app.UseMiddleware<UserRoleClaimsMiddleware>();",
            "app.UseRateLimiter();",
            "app.UseCors();",
            "app.UseAuthorization();",
            "app.MapControllers();",
            "app.Map(\"/api/{**path}\"",
            "app.MapFallbackToFile(\"index.html\");");
    }

    [TestMethod]
    public void AuthorizationDefaultPolicyRequiresAuthenticatedUserAndApiScope()
    {
        var program = ReadProgramSource();

        StringAssert.Contains(program, "options.DefaultPolicy");
        StringAssert.Contains(program, ".RequireAuthenticatedUser()");
        StringAssert.Contains(program, "ApiScopeAuthorization.HasRequiredScope(context.User, requiredApiScope)");
    }

    private static void AssertInOrder(string text, params string[] snippets)
    {
        var previousIndex = -1;
        foreach (var snippet in snippets)
        {
            var index = text.IndexOf(snippet, StringComparison.Ordinal);
            Assert.IsTrue(index >= 0, $"Could not find '{snippet}' in Program.cs.");
            Assert.IsTrue(index > previousIndex, $"'{snippet}' must appear after the previous middleware/route guardrail.");
            previousIndex = index;
        }
    }

    private static string ReadProgramSource()
        => File.ReadAllText(Path.Combine(GetRepositoryRoot(), "Fleet.Server", "Program.cs"));

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Fleet.Server", "Program.cs");
            if (File.Exists(candidate))
                return current.FullName;

            current = current.Parent;
        }

        Assert.Fail("Could not locate Fleet.Server/Program.cs from the test output directory.");
        return string.Empty;
    }
}
