using Microsoft.Extensions.Options;

namespace Fleet.Server.LLM;

/// <summary>
/// Decorates an <see cref="ILLMClient"/> with automatic model fallback.
/// When the primary model returns a retryable error (rate-limited, server error),
/// the request is retried once with <see cref="LLMOptions.FallbackModel"/>.
/// </summary>
public class ResilientLLMClient(
    ILLMClient inner,
    IOptions<LLMOptions> options,
    ILogger<ResilientLLMClient> logger) : ILLMClient
{
    public async Task<LLMResponse> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.CompleteAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsRetryableError(ex) && CanFallback(request))
        {
            var fallback = options.Value.FallbackModel!;
            logger.LogWarning(
                "Primary model failed with retryable error. Falling back to {FallbackModel}. Error: {Error}",
                fallback, ex.Message);

            var fallbackRequest = request with { ModelOverride = fallback };
            return await inner.CompleteAsync(fallbackRequest, cancellationToken);
        }
        catch (TimeoutException ex) when (CanFallback(request))
        {
            var fallback = options.Value.FallbackModel!;
            logger.LogWarning(
                "Primary model timed out. Falling back to {FallbackModel}. Error: {Error}",
                fallback, ex.Message);

            var fallbackRequest = request with { ModelOverride = fallback };
            return await inner.CompleteAsync(fallbackRequest, cancellationToken);
        }
    }

    private bool CanFallback(LLMRequest request)
    {
        var fallbackModel = options.Value.FallbackModel;
        if (string.IsNullOrWhiteSpace(fallbackModel))
            return false;

        // Don't fall back if already using the fallback model
        if (string.Equals(request.ModelOverride, fallbackModel, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool IsRetryableError(InvalidOperationException ex)
    {
        var message = ex.Message;
        // Check for HTTP status codes that indicate retryable failures
        return message.Contains("429", StringComparison.Ordinal) ||
               message.Contains("500", StringComparison.Ordinal) ||
               message.Contains("502", StringComparison.Ordinal) ||
               message.Contains("503", StringComparison.Ordinal) ||
               message.Contains("504", StringComparison.Ordinal) ||
               message.Contains("overloaded", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }
}
