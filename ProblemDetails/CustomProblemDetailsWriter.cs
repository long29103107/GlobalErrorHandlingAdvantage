using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GlobalErrorHandlingAdvantage.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace GlobalErrorHandlingAdvantage.ProblemDetails;

public sealed class CustomProblemDetailsWriter : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context)
    {
        return context.HttpContext.Request.Headers.Accept
            .Any(h => h?.Contains("application/json") == true);
    }
    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        var problemDetails = context.ProblemDetails;
        // Add custom metadata
        problemDetails.Extensions["machineName"] = Environment.MachineName;
        problemDetails.Extensions["version"] = Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString();
        // Custom serialization options
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, options));
    }
}
// Enhanced exception handler using IProblemDetailsService
public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred");
        var problemDetailsContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = CreateProblemDetails(httpContext, exception),
            Exception = exception
        };
        await problemDetailsService.WriteAsync(problemDetailsContext);
        return true;
    }
    private static Microsoft.AspNetCore.Mvc.ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var (statusCode, title, type) = exception switch
        {
            ValidationException => (400, "Validation Error", "validation-error"),
            BusinessRuleException => (422, "Business Rule Violation", "business-rule-violation"),
            UnauthorizedAccessException => (401, "Unauthorized", "unauthorized"),
            _ => (500, "Internal Server Error", "internal-error")
        };
        var details = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://myapp.com/errors/{type}",
            Detail = GetSafeErrorMessage(exception)
        };
        details.Instance = context.Request.Path;
        return details;
    }
    private static string GetSafeErrorMessage(Exception exception)
    {
        return exception switch
        {
            ValidationException or BusinessRuleException => exception.Message,
            _ => "An unexpected error occurred while processing your request."
        };
    }
}