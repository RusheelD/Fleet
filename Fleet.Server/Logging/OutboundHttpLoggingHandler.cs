using System.Diagnostics;

namespace Fleet.Server.Logging;

public class OutboundHttpLoggingHandler(ILogger<OutboundHttpLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var target = LogSanitizer.SanitizeUrl(request.RequestUri?.ToString() ?? "unknown");

        logger.OutboundHttpStarted(request.Method.Method, target.SanitizeForLogging());

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            logger.OutboundHttpCompleted(
                request.Method.Method,
                target.SanitizeForLogging(),
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.OutboundHttpFailed(ex, request.Method.Method, target.SanitizeForLogging(), stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
