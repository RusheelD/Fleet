namespace Fleet.Server.LLM;

/// <summary>
/// Provider-agnostic LLM client. Implement this once per provider
/// (Gemini, OpenAI, Anthropic, Ollama) and swap via configuration.
/// </summary>
public interface ILLMClient
{
    /// <summary>Send a completion request and get the model's response.</summary>
    Task<LLMResponse> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default);
}
