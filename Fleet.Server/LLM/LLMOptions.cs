namespace Fleet.Server.LLM;

/// <summary>Configuration for the active LLM provider.</summary>
public class LLMOptions
{
    public const string SectionName = "LLM";

    public string Provider { get; set; } = "claude";
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Default model used for normal chat (e.g., claude-haiku-4-20250514).</summary>
    public string Model { get; set; } = "claude-haiku-4-20250514";
    /// <summary>Stronger model used for work-item generation (e.g., claude-sonnet-4-20250514).</summary>
    public string GenerateModel { get; set; } = "claude-haiku-4-20250514";
    public int MaxToolLoops { get; set; } = 25;
    public int MaxToolCallsTotal { get; set; } = 50;
    public int TimeoutSeconds { get; set; } = 180;
    /// <summary>Timeout for work-item generation requests (longer because many tools may be called).</summary>
    public int GenerateTimeoutSeconds { get; set; } = 1800;
    /// <summary>Max tool loops for work-item generation (higher to allow many creates).</summary>
    public int GenerateMaxToolLoops { get; set; } = 200;
    public int MaxToolOutputLength { get; set; } = 24000;
}
