namespace Fleet.Server.LLM;

/// <summary>Normalized request sent to any LLM provider.</summary>
public record LLMRequest(
    string SystemPrompt,
    IReadOnlyList<LLMMessage> Messages,
    IReadOnlyList<LLMToolDefinition>? Tools = null,
    string? ModelOverride = null
);

/// <summary>Normalized response from any LLM provider.</summary>
public record LLMResponse(
    string? Content,
    IReadOnlyList<LLMToolCall>? ToolCalls
);

/// <summary>Normalized chat message (user, assistant, or tool result).</summary>
public record LLMMessage
{
    public required string Role { get; init; }
    public string? Content { get; init; }
    public IReadOnlyList<LLMToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
}

/// <summary>A tool invocation requested by the LLM.</summary>
public record LLMToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>Schema definition for a tool the LLM can invoke.</summary>
public record LLMToolDefinition(string Name, string Description, string ParametersJsonSchema);

/// <summary>Configuration for the active LLM provider.</summary>
public class LLMOptions
{
    public const string SectionName = "LLM";

    public string Provider { get; set; } = "claude";
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Default model used for normal chat (e.g., claude-haiku-4-20250514).</summary>
    public string Model { get; set; } = "claude-haiku-4-20250514";
    /// <summary>Stronger model used for work-item generation (e.g., claude-sonnet-4-20250514).</summary>
    public string GenerateModel { get; set; } = "claude-sonnet-4-20250514";
    public int MaxToolLoops { get; set; } = 10;
    public int MaxToolCallsTotal { get; set; } = 20;
    public int TimeoutSeconds { get; set; } = 180;
    public int MaxToolOutputLength { get; set; } = 8000;
}
