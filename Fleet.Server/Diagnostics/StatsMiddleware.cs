using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Fleet.Server.Diagnostics;

public sealed class StatsMiddleware(
    RequestDelegate next,
    ServiceStats stats)
{
    private const string StatsPath = "/_stats";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
            string.Equals(context.Request.Path.Value, StatsPath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json;charset=utf-8";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(stats.CreateSnapshot(), JsonSerializerOptions),
                Encoding.UTF8);
            return;
        }

        stats.RecordRequestStarted(context.Request.Method);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
            stopwatch.Stop();
            stats.RecordRequestCompleted(context.Response.StatusCode, stopwatch.ElapsedMilliseconds, failed: false);
        }
        catch
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode >= StatusCodes.Status400BadRequest
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;
            stats.RecordRequestCompleted(statusCode, stopwatch.ElapsedMilliseconds, failed: true);
            throw;
        }
    }
}
