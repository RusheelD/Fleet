namespace Fleet.Server.LLM;

/// <summary>Normalized request sent to any LLM provider.</summary>
public record LLMRequest(
    string SystemPrompt,
    IReadOnlyList<LLMMessage> Messages,
    IReadOnlyList<LLMToolDefinition>? Tools = null,
    string? ModelOverride = null,
    int? MaxTokens = null,
    /// <summary>
    /// When true, marks the first user message with cache_control so the provider
    /// caches everything up to (and including) that message. Useful when the first
    /// user message contains stable context resent every tool-loop iteration.
    /// </summary>
    bool CacheFirstUserMessage = false
);
