using Microsoft.Extensions.Logging;

namespace Fleet.Server.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception. traceId={traceId}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string traceId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Unhandled exception details. traceId={traceId} details={details}")]
    public static partial void UnhandledExceptionDetails(this ILogger logger, string traceId, string details);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "HTTP request started. method={method} path={path} query={query} traceId={traceId} userId={userId}")]
    public static partial void HttpRequestStarted(this ILogger logger, string method, string path, string query, string traceId, string userId);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "HTTP request completed. statusCode={statusCode} elapsedMs={elapsedMs} traceId={traceId}")]
    public static partial void HttpRequestCompleted(this ILogger logger, int statusCode, long elapsedMs, string traceId);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "HTTP request failed. elapsedMs={elapsedMs} traceId={traceId}")]
    public static partial void HttpRequestFailed(this ILogger logger, Exception exception, long elapsedMs, string traceId);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "HTTP request canceled by client. elapsedMs={elapsedMs} traceId={traceId}")]
    public static partial void HttpRequestCanceled(this ILogger logger, long elapsedMs, string traceId);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Action started. actionName={actionName} traceId={traceId}")]
    public static partial void ActionStarted(this ILogger logger, string actionName, string traceId);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Action completed. actionName={actionName} statusCode={statusCode} elapsedMs={elapsedMs}")]
    public static partial void ActionCompleted(this ILogger logger, string actionName, int statusCode, long elapsedMs);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Error, Message = "Action failed. actionName={actionName} elapsedMs={elapsedMs}")]
    public static partial void ActionFailed(this ILogger logger, Exception exception, string actionName, long elapsedMs);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Action canceled by client. actionName={actionName} elapsedMs={elapsedMs}")]
    public static partial void ActionCanceled(this ILogger logger, string actionName, long elapsedMs);

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Outbound HTTP started. method={method} target={target}")]
    public static partial void OutboundHttpStarted(this ILogger logger, string method, string target);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Information, Message = "Outbound HTTP completed. method={method} target={target} statusCode={statusCode} elapsedMs={elapsedMs}")]
    public static partial void OutboundHttpCompleted(this ILogger logger, string method, string target, int statusCode, long elapsedMs);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Error, Message = "Outbound HTTP failed. method={method} target={target} elapsedMs={elapsedMs}")]
    public static partial void OutboundHttpFailed(this ILogger logger, Exception exception, string method, string target, long elapsedMs);
}
