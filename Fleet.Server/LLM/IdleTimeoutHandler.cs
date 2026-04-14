using System.Net;
using System.Net.Http;

namespace Fleet.Server.LLM;

/// <summary>
/// Reads an HTTP response body with an idle timeout. If no bytes arrive within
/// the idle timeout, the read is canceled. This catches stale connections that
/// the overall HTTP timeout misses (for example, the server accepted the
/// request but stopped sending mid-response).
/// On mid-stream idle timeout, automatically retries with a buffered read
/// before giving up.
/// </summary>
public static class IdleTimeoutHandler
{
    /// <summary>Default idle timeout between chunks (90 seconds).</summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(90);
    /// <summary>Default max wait for response headers / first byte.</summary>
    public static readonly TimeSpan DefaultResponseHeadersTimeout = TimeSpan.FromSeconds(75);

    /// <summary>
    /// Sends an HTTP request and reads the full response body with idle-timeout
    /// protection. If the streaming read stalls after headers are received,
    /// retries once using a standard buffered read as a fallback.
    /// </summary>
    public static async Task<(HttpStatusCode StatusCode, string Body)> SendWithIdleTimeoutAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        TimeSpan? idleTimeout = null)
    {
        var timeout = idleTimeout ?? DefaultIdleTimeout;
        var responseHeadersTimeout = timeout < DefaultResponseHeadersTimeout
            ? timeout
            : DefaultResponseHeadersTimeout;

        try
        {
            return await ReadWithStreamingIdleDetectionAsync(
                httpClient,
                request,
                responseHeadersTimeout,
                timeout,
                cancellationToken);
        }
        catch (ResponseHeadersTimeoutException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            // Streaming read stalled after headers arrived. Retry with a standard
            // non-streaming read before bubbling the failure up to the caller.
            using var retryRequest = await CloneRequestAsync(request, cancellationToken);
            return await ReadWithBufferedFallbackAsync(httpClient, retryRequest, timeout, cancellationToken);
        }
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> ReadWithStreamingIdleDetectionAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        TimeSpan responseHeadersTimeout,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken)
    {
        using var responseHeadersCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseHeadersCts.CancelAfter(responseHeadersTimeout);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                responseHeadersCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ResponseHeadersTimeoutException(
                $"LLM response headers were not received for {responseHeadersTimeout.TotalSeconds:F0}s. " +
                "The upstream provider may be overloaded or unreachable.");
        }

        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();

            var buffer = new byte[8192];
            while (true)
            {
                using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                idleCts.CancelAfter(idleTimeout);

                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, idleCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"LLM response stalled - no data received for {idleTimeout.TotalSeconds:F0}s. " +
                        "The upstream provider may be overloaded.");
                }

                if (bytesRead == 0)
                    break;

                memoryStream.Write(buffer, 0, bytesRead);
            }

            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream);
            var body = await reader.ReadToEndAsync(cancellationToken);
            return (response.StatusCode, body);
        }
    }

    /// <summary>
    /// Fallback: send the request without streaming (let HttpClient buffer the
    /// whole response). Simpler but it won't detect mid-stream stalls. Uses the
    /// overall timeout instead.
    /// </summary>
    private static async Task<(HttpStatusCode StatusCode, string Body)> ReadWithBufferedFallbackAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout * 3);

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            return (response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "LLM response failed on both streaming and buffered reads. " +
                "The upstream provider may be overloaded or unreachable.");
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content is not null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private sealed class ResponseHeadersTimeoutException(string message) : TimeoutException(message);
}
