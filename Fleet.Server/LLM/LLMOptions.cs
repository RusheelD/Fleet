namespace Fleet.Server.LLM;

/// <summary>Configuration for the active LLM provider.</summary>
public class LLMOptions
{
    public const string SectionName = "LLM";

    public string Provider { get; set; } = "azure-openai";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Default model used for normal chat (e.g., gpt-5.2-codex).</summary>
    public string Model { get; set; } = "gpt-5.2-codex";
    /// <summary>Model used for work-item generation.</summary>
    public string GenerateModel { get; set; } = "gpt-5.2-codex";
    public int MaxToolLoops { get; set; } = 25;
    public int MaxToolCallsTotal { get; set; } = 50;
    public int TimeoutSeconds { get; set; } = 1800;
    /// <summary>Timeout for work-item generation requests (longer because many tools may be called).</summary>
    public int GenerateTimeoutSeconds { get; set; } = 7200;
    /// <summary>Max tool loops for work-item generation (higher to allow many creates).</summary>
    public int GenerateMaxToolLoops { get; set; } = 400;
    /// <summary>Max non-write tool calls for work-item generation before the run is capped.</summary>
    public int GenerateMaxToolCallsTotal { get; set; } = 400;
    /// <summary>
    /// Hard app-wide ceiling for concurrent autonomous agent model turns.
    /// Runs wait for capacity when this ceiling is saturated.
    /// </summary>
    public int MaxConcurrentAgentCalls { get; set; } = 8;
    public int MaxToolOutputLength { get; set; } = 24000;
    /// <summary>
    /// Maximum aggregate characters of tool output per LLM turn. When many tools
    /// fire in one response, later results are progressively truncated once the
    /// budget is consumed. 0 = unlimited.
    /// </summary>
    public int MaxAggregateToolOutputChars { get; set; } = 200_000;
    /// <summary>Context window size in tokens for context compression.</summary>
    public int ContextWindowTokens { get; set; } = 200000;
    /// <summary>Tokens reserved for model output when computing compression budget.</summary>
    public int ReservedOutputTokens { get; set; } = 16384;
    /// <summary>Fallback model to use when the primary model fails with a retryable error (429, 500+).</summary>
    public string? FallbackModel { get; set; }
}
