using Microsoft.AspNetCore.Diagnostics;

namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class SecurityAwareExceptionHandler(
    IWebHostEnvironment environment,
    ILogger<SecurityAwareExceptionHandler> logger)
    : IExceptionHandler
{
    private static readonly HashSet<Type> SafeExceptionTypes = new()
    {
        typeof(ValidationException),
        typeof(ArgumentException),
        typeof(BusinessRuleException)
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Always log full exception details for internal monitoring
        logger.LogError(exception, 
            "Exception occurred. User: {UserId}, IP: {IPAddress}, UserAgent: {UserAgent}",
            httpContext.User.Identity?.Name ?? "Anonymous",
            httpContext.Connection.RemoteIpAddress,
            httpContext.Request.Headers.UserAgent.FirstOrDefault());
        var problemDetails = CreateSecureProblemDetails(httpContext, exception);
        
        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        
        return true;
    }
    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateSecureProblemDetails(HttpContext context, Exception exception)
    {
        var isSafeException = SafeExceptionTypes.Contains(exception.GetType());
        var isDevEnvironment = environment.IsDevelopment();
        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = GetStatusCode(exception),
            Title = GetTitle(exception),
            Detail = GetSecureDetail(exception, isSafeException, isDevEnvironment),
            Instance = context.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier,
                ["timestamp"] = DateTime.UtcNow
                // Never include sensitive information like stack traces in production
            }
        };
    }
    private static string GetSecureDetail(Exception exception, bool isSafeException, bool isDevEnvironment)
    {
        // Only expose exception details for safe exceptions or in development
        if (isSafeException || isDevEnvironment)
        {
            return exception.Message;
        }
        // Generic message for potentially sensitive exceptions in production
        return "An error occurred while processing your request. Please contact support if the problem persists.";
    }
    private static int GetStatusCode(Exception exception) => exception switch
    {
        ValidationException => 400,
        BusinessRuleException => 422,
        UnauthorizedAccessException => 401,
        _ => 500
    };
    private static string GetTitle(Exception exception) => exception switch
    {
        ValidationException => "Validation Error",
        BusinessRuleException => "Business Rule Violation",
        UnauthorizedAccessException => "Unauthorized",
        _ => "Internal Server Error"
    };
}