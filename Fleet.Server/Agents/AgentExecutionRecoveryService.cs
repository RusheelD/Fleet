using Microsoft.Extensions.Options;

namespace Fleet.Server.Agents;

public sealed class AgentExecutionRecoveryService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<AgentExecutionRecoveryOptions> options,
    ILogger<AgentExecutionRecoveryService> logger) : BackgroundService
{
    private readonly AgentExecutionRecoveryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.StartupDelay > TimeSpan.Zero)
            await Task.Delay(_options.StartupDelay, stoppingToken);

        var attempts = 0;
        while (!stoppingToken.IsCancellationRequested && attempts < Math.Max(1, _options.MaxAttempts))
        {
            attempts++;
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var orchestrationService = scope.ServiceProvider.GetRequiredService<IAgentOrchestrationService>();
                var recoveredCount = await orchestrationService.RecoverInterruptedExecutionsAsync(stoppingToken);

                logger.LogInformation(
                    "Interrupted execution recovery pass {Attempt}/{MaxAttempts} completed; recovered {RecoveredCount} top-level execution(s).",
                    attempts,
                    Math.Max(1, _options.MaxAttempts),
                    recoveredCount);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Interrupted execution recovery pass {Attempt}/{MaxAttempts} failed.",
                    attempts,
                    Math.Max(1, _options.MaxAttempts));

                if (attempts >= Math.Max(1, _options.MaxAttempts) || _options.RetryDelay <= TimeSpan.Zero)
                    break;

                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }
    }
}
