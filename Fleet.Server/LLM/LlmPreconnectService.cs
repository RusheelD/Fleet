using Microsoft.Extensions.Options;

namespace Fleet.Server.LLM;

/// <summary>
/// Background service that pre-warms the TCP+TLS connection to Azure OpenAI during
/// app startup. Sends a lightweight HEAD request to the configured endpoint so that
/// the first real LLM request doesn't pay the cold-start overhead.
/// Inspired by Claude Code's API preconnection pattern (Ch7).
/// </summary>
public sealed class LlmPreconnectService(
    IHttpClientFactory httpClientFactory,
    IOptions<LLMOptions> options,
    ILogger<LlmPreconnectService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            logger.LogDebug("LLM endpoint not configured — skipping preconnect");
            return;
        }

        // Extract the origin (scheme + host) from the full endpoint URL
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpointUri))
        {
            logger.LogWarning("LLM endpoint is not a valid URI — skipping preconnect: {Endpoint}",
                config.Endpoint);
            return;
        }

        var origin = $"{endpointUri.Scheme}://{endpointUri.Authority}";

        try
        {
            using var httpClient = httpClientFactory.CreateClient("LLM");
            using var request = new HttpRequestMessage(HttpMethod.Head, origin);

            // We don't care about the response — the goal is to establish TCP+TLS
            logger.LogInformation("Pre-warming connection to Azure OpenAI at {Origin}", origin);
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            logger.LogInformation("LLM preconnect completed: {StatusCode}", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // App is shutting down during startup — don't log a warning
        }
        catch (Exception ex)
        {
            // Non-fatal: log and continue — the connection will be established on first use
            logger.LogWarning(ex, "LLM preconnect failed (non-fatal, will connect on first request)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
