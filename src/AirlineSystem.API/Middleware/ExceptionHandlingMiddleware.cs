using AirlineSystem.Application.Exceptions;
using System.Text.Json;

namespace AirlineSystem.API.Middleware;

/// <summary>
/// Global exception-handling middleware. Intercepts all unhandled exceptions
/// thrown by downstream middleware and controllers, maps them to appropriate
/// HTTP status codes, and returns a consistent JSON error envelope so that
/// clients never receive an empty 500 or an ASP.NET HTML error page.
/// </summary>
/// <remarks>
/// <b>Error envelope shape:</b>
/// <code>
/// { "statusCode": 404, "message": "Flight 'TK999' on 2025-06-01 was not found." }
/// </code>
/// <b>Exception → status mapping:</b>
/// <list type="table">
///   <listheader><term>Exception</term><description>HTTP Status</description></listheader>
///   <item><term><see cref="KeyNotFoundException"/></term><description>404 Not Found</description></item>
///   <item><term><see cref="UnauthorizedAccessException"/></term><description>401 Unauthorized</description></item>
///   <item><term><see cref="InvalidOperationException"/></term><description>400 Bad Request</description></item>
///   <item><term><see cref="ArgumentException"/></term><description>400 Bad Request</description></item>
///   <item><term><see cref="AirlineSystem.Application.Exceptions.ConcurrencyConflictException"/></term><description>409 Conflict</description></item>
///   <item><term>Any other <see cref="Exception"/></term><description>500 Internal Server Error (generic message; full exception logged)</description></item>
/// </list>
/// Register this middleware <b>first</b> in the pipeline so it wraps all
/// subsequent layers:
/// <code>app.UseMiddleware&lt;ExceptionHandlingMiddleware&gt;();</code>
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Initialises the middleware with the next delegate and a logger.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger used to record unexpected 500-level exceptions.</param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware: passes the request downstream and catches any
    /// exception to produce a structured JSON error response.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode;
        string message;

        switch (exception)
        {
            case KeyNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                message    = exception.Message;
                break;

            case UnauthorizedAccessException:
                statusCode = StatusCodes.Status401Unauthorized;
                message    = exception.Message;
                break;

            case InvalidOperationException:
            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                message    = exception.Message;
                break;

            case ConcurrencyConflictException:
                statusCode = StatusCodes.Status409Conflict;
                message    = exception.Message;
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                message    = "An unexpected error occurred.";
                _logger.LogError(exception,
                    "Unhandled exception on {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = statusCode;

        var body = JsonSerializer.Serialize(new
        {
            statusCode,
            message
        });

        await context.Response.WriteAsync(body);
    }
}
