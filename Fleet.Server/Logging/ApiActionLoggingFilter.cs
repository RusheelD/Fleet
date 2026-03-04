using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fleet.Server.Logging;

public class ApiActionLoggingFilter(ILogger<ApiActionLoggingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.DisplayName ?? "UnknownAction";
        var traceId = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        var routeValues = LogSanitizer.SanitizeRouteValues(context.RouteData.Values);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["ActionName"] = actionName,
            ["RouteValues"] = routeValues
        });

        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            var errorsJson = JsonSerializer.Serialize(errors).SanitizeForLogging();
            logger.ActionValidationFailed(actionName.SanitizeForLogging(), traceId, errorsJson);

            context.Result = new BadRequestObjectResult(context.ModelState);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        logger.ActionStarted(actionName.SanitizeForLogging(), traceId);

        var executedContext = await next();

        stopwatch.Stop();

        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            logger.ActionFailed(executedContext.Exception, actionName.SanitizeForLogging(), stopwatch.ElapsedMilliseconds);
            return;
        }

        var statusCode = executedContext.HttpContext.Response.StatusCode;
        logger.ActionCompleted(actionName.SanitizeForLogging(), statusCode, stopwatch.ElapsedMilliseconds);
    }
}
