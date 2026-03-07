using System.Diagnostics;
using System.Security.Claims;

namespace Fleet.Server.Logging;

public class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var sanitizedQuery = LogSanitizer.SanitizeQueryString(context.Request.QueryString.ToString());
        var userId = context.User.FindFirst("oid")?.Value
            ?? context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";

        context.Response.Headers["X-Trace-Id"] = traceId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = traceId,
            ["UserId"] = userId,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.ToString(),
            ["Query"] = sanitizedQuery
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.HttpRequestStarted(
                context.Request.Method,
                context.Request.Path.ToString().SanitizeForLogging(),
                sanitizedQuery.SanitizeForLogging(),
                traceId,
                userId);

            await next(context);
            stopwatch.Stop();

            logger.HttpRequestCompleted(
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                traceId);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.HttpRequestCanceled(stopwatch.ElapsedMilliseconds, traceId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.HttpRequestFailed(ex, stopwatch.ElapsedMilliseconds, traceId);
            throw;
        }
    }
}
