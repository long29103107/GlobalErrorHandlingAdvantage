using System.Text.Json;
using GlobalErrorHandlingAdvantage.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace GlobalErrorHandlingAdvantage.Middlewares;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, 
            "Unhandled exception occurred. TraceId: {TraceId}", 
            context.TraceIdentifier);
        // Prevent multiple error responses
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Cannot write error response. Response has already started.");
            return;
        }
        var problemDetails = CreateProblemDetails(context, exception);
        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = environment.IsDevelopment()
        };
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, options));
    }
    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => new ValidationProblemDetails(validationEx.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = validationEx.Message,
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            },
            
            BusinessRuleException businessEx => new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business Rule Violation",
                Detail = businessEx.Message,
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            },
            
            _ => new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = environment.IsDevelopment() 
                    ? exception.Message 
                    : "An unexpected error occurred.",
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        };
    }
}