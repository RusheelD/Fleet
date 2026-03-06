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
    public int TimeoutSeconds { get; set; } = 180;
    /// <summary>Timeout for work-item generation requests (longer because many tools may be called).</summary>
    public int GenerateTimeoutSeconds { get; set; } = 1800;
    /// <summary>Max tool loops for work-item generation (higher to allow many creates).</summary>
    public int GenerateMaxToolLoops { get; set; } = 200;
    public int MaxToolOutputLength { get; set; } = 24000;
}

