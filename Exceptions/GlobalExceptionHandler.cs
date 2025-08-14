using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    private readonly IHostEnvironment _env = env;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Log the exception with structured logging
        logger.LogError(exception, "Unhandled exception occurred. TraceIdentifier: {TraceIdentifier}", 
            httpContext.TraceIdentifier);
        // Create problem details response
        var problemDetails = CreateProblemDetails(httpContext, exception);
        // Set response properties
        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        httpContext.Response.ContentType = "application/problem+json";
        // Write the response
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true; // Exception handled successfully
    }
    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var statusCode = GetStatusCode(exception);
        
        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Type = GetProblemType(exception),
            Title = GetTitle(exception),
            Detail = GetDetail(exception),
            Instance = context.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier,
                ["timestamp"] = DateTime.UtcNow
            }
        };
    }
    private int GetStatusCode(Exception exception) => exception switch
    {
        ArgumentException => StatusCodes.Status400BadRequest,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        NotImplementedException => StatusCodes.Status501NotImplemented,
        TimeoutException => StatusCodes.Status408RequestTimeout,
        _ => StatusCodes.Status500InternalServerError
    };
    private string GetProblemType(Exception exception) => exception switch
    {
        ArgumentException => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
        UnauthorizedAccessException => "https://datatracker.ietf.org/doc/html/rfc7235#section-3.1",
        NotImplementedException => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.2",
        TimeoutException => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.7",
        _ => "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
    };
    private string GetTitle(Exception exception) => exception switch
    {
        ArgumentException => "Bad Request",
        UnauthorizedAccessException => "Unauthorized",
        NotImplementedException => "Not Implemented",
        TimeoutException => "Request Timeout",
        _ => "Internal Server Error"
    };
    private string GetDetail(Exception exception)
    {
        // In production, avoid exposing detailed exception messages
        return _env.IsDevelopment() 
            ? exception.Message 
            : "An error occurred while processing your request.";
    }
}