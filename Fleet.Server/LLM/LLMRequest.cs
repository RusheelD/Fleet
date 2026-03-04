namespace Fleet.Server.LLM;

/// <summary>Normalized request sent to any LLM provider.</summary>
public record LLMRequest(
    string SystemPrompt,
    IReadOnlyList<LLMMessage> Messages,
    IReadOnlyList<LLMToolDefinition>? Tools = null,
    string? ModelOverride = null
);
