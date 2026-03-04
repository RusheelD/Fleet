using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Fleet.Server.Logging;

namespace Fleet.Server.Exceptions;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IWebHostEnvironment environment,
    IConfiguration configuration) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var includeExceptionDetailsInResponse =
            environment.IsDevelopment() ||
            configuration.GetValue<bool>("Diagnostics:IncludeExceptionDetailsInResponse");

        logger.UnhandledException(exception, httpContext.TraceIdentifier);
        logger.UnhandledExceptionDetails(httpContext.TraceIdentifier, exception.ToString().SanitizeForLogging());

        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = exception switch
            {
                ArgumentException => "Bad Request",
                KeyNotFoundException => "Not Found",
                UnauthorizedAccessException => "Unauthorized",
                InvalidOperationException => "Conflict",
                _ => "Internal Server Error"
            },
            Detail = includeExceptionDetailsInResponse
                ? exception.ToString()
                : statusCode == StatusCodes.Status500InternalServerError
                    ? "An internal server error occurred."
                    : exception.Message,
            Instance = httpContext.Request.Path,
            Extensions = { ["traceId"] = httpContext.TraceIdentifier }
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
