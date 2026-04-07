namespace Fleet.Server.Agents;

public sealed class AgentExecutionRecoveryOptions
{
    public const string SectionName = "AgentExecutionRecovery";

    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(20);
    public int MaxAttempts { get; set; } = 6;
}
