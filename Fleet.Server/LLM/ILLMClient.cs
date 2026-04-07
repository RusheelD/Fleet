namespace Fleet.Server.LLM;

/// <summary>
/// Provider-agnostic LLM client. Implement this per provider
/// (e.g. Azure OpenAI) and swap via configuration.
/// </summary>
public interface ILLMClient
{
    /// <summary>Send a completion request and get the model's response.</summary>
    Task<LLMResponse> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default);
}
