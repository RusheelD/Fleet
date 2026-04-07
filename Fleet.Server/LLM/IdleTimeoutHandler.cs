using System.Net;
using System.Net.Http;

namespace Fleet.Server.LLM;

/// <summary>
/// Reads an HTTP response body with an idle timeout — if no bytes arrive
/// within the idle timeout, the read is canceled.
/// This catches stale connections that the overall HTTP timeout misses
/// (e.g., server accepted the request but stopped sending mid-response).
/// </summary>
public static class IdleTimeoutHandler
{
    /// <summary>Default idle timeout between chunks (90 seconds).</summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Sends an HTTP request and reads the full response body with idle-timeout
    /// protection. Returns both the status code and the response body string so
    /// callers can perform their own error handling.
    /// </summary>
    public static async Task<(HttpStatusCode StatusCode, string Body)> SendWithIdleTimeoutAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        TimeSpan? idleTimeout = null)
    {
        var timeout = idleTimeout ?? DefaultIdleTimeout;

        // Read headers first so we can stream the body with idle detection
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memoryStream = new MemoryStream();

        var buffer = new byte[8192];
        while (true)
        {
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idleCts.CancelAfter(timeout);

            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, idleCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"LLM response stalled — no data received for {timeout.TotalSeconds:F0}s. " +
                    "The upstream provider may be overloaded.");
            }

            if (bytesRead == 0) break;
            memoryStream.Write(buffer, 0, bytesRead);
        }

        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var body = await reader.ReadToEndAsync(cancellationToken);

        return (response.StatusCode, body);
    }
}
