namespace Fleet.Server.LLM;

/// <summary>Normalized request sent to any LLM provider.</summary>
public record LLMRequest(
    string SystemPrompt,
    IReadOnlyList<LLMMessage> Messages,
    IReadOnlyList<LLMToolDefinition>? Tools = null
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

    public string Provider { get; set; } = "gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public int MaxToolLoops { get; set; } = 5;
    public int MaxToolCallsTotal { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxToolOutputLength { get; set; } = 4000;
}
