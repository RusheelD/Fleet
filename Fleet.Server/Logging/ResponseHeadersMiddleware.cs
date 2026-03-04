using System.Diagnostics;

namespace Fleet.Server.Logging;

public class ResponseHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        var operationId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
        var requestId = activity?.Id ?? context.TraceIdentifier;
        var spanId = activity?.SpanId.ToString() ?? string.Empty;

        context.Response.Headers["x-operation-id"] = operationId;
        context.Response.Headers["x-request-id"] = requestId;

        if (!string.IsNullOrWhiteSpace(spanId))
        {
            context.Response.Headers["x-span-id"] = spanId;
        }

        await next(context);
    }
}
