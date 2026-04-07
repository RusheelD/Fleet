using Fleet.Server.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentExecutionRecoveryServiceTests
{
    [TestMethod]
    public async Task StartAsync_RunsRecoveryPassAfterStartupDelay()
    {
        var orchestrationService = new Mock<IAgentOrchestrationService>();
        var recoveryCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrationService
            .Setup(service => service.RecoverInterruptedExecutionsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => recoveryCalled.TrySetResult())
            .ReturnsAsync(2);

        await using var provider = BuildProvider(orchestrationService.Object);
        var service = new AgentExecutionRecoveryService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AgentExecutionRecoveryOptions
            {
                StartupDelay = TimeSpan.Zero,
                RetryDelay = TimeSpan.Zero,
                MaxAttempts = 1,
            }),
            NullLogger<AgentExecutionRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await recoveryCalled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await service.StopAsync(CancellationToken.None);

        orchestrationService.Verify(
            candidate => candidate.RecoverInterruptedExecutionsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StartAsync_RetriesRecoveryWhenFirstAttemptFails()
    {
        var orchestrationService = new Mock<IAgentOrchestrationService>();
        var attemptCount = 0;
        var recoveryCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrationService
            .Setup(service => service.RecoverInterruptedExecutionsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(_ =>
            {
                attemptCount++;
                if (attemptCount == 1)
                    throw new InvalidOperationException("Transient startup failure.");

                recoveryCompleted.TrySetResult();
                return Task.FromResult(1);
            });

        await using var provider = BuildProvider(orchestrationService.Object);
        var service = new AgentExecutionRecoveryService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AgentExecutionRecoveryOptions
            {
                StartupDelay = TimeSpan.Zero,
                RetryDelay = TimeSpan.FromMilliseconds(10),
                MaxAttempts = 2,
            }),
            NullLogger<AgentExecutionRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await recoveryCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await service.StopAsync(CancellationToken.None);

        orchestrationService.Verify(
            candidate => candidate.RecoverInterruptedExecutionsAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static ServiceProvider BuildProvider(IAgentOrchestrationService orchestrationService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => orchestrationService);
        return services.BuildServiceProvider();
    }
}
