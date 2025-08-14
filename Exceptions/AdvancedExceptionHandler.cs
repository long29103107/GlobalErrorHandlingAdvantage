using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class AdvancedExceptionHandler(
    ILogger<AdvancedExceptionHandler> logger,
    IWebHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogException(exception, httpContext);
        var problemDetails = exception switch
        {
            ValidationException validationEx => CreateValidationProblemDetails(httpContext, validationEx),
            DomainException domainEx => CreateDomainProblemDetails(httpContext, domainEx),
            _ => CreateGenericProblemDetails(httpContext, exception)
        };
        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
    private void LogException(Exception exception, HttpContext context)
    {
        var logLevel = exception switch
        {
            DomainException => LogLevel.Warning,
            _ => LogLevel.Error
        };
        logger.Log(logLevel, exception,
            "Exception occurred. Type: {ExceptionType}, TraceId: {TraceId}, Path: {Path}",
            exception.GetType().Name,
            context.TraceIdentifier,
            context.Request.Path);
    }
    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateValidationProblemDetails(HttpContext context, ValidationException exception)
    {
        var problemDetails = new ValidationProblemDetails(exception.Errors)
        {
            Status = exception.StatusCode,
            Type = $"https://myapp.com/errors/{exception.ErrorType}",
            Title = "Validation Error",
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        AddCommonExtensions(problemDetails, context);
        return problemDetails;
    }
    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateDomainProblemDetails(HttpContext context, DomainException exception)
    {
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = exception.StatusCode,
            Type = $"https://myapp.com/errors/{exception.ErrorType}",
            Title = exception.GetType().Name.Replace("Exception", ""),
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        AddCommonExtensions(problemDetails, context);
        return problemDetails;
    }
    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateGenericProblemDetails(HttpContext context, Exception exception)
    {
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Title = "Internal Server Error",
            Detail = environment.IsDevelopment() 
                ? exception.Message 
                : "An unexpected error occurred.",
            Instance = context.Request.Path
        };
        AddCommonExtensions(problemDetails, context);
        return problemDetails;
    }
    private static void AddCommonExtensions(Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails, HttpContext context)
    {
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
        problemDetails.Extensions["requestId"] = context.Items["RequestId"]?.ToString();
    }
}