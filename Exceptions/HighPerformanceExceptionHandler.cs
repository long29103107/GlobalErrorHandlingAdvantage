using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;

namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class HighPerformanceExceptionHandler(ILogger<HighPerformanceExceptionHandler> logger) : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Fast path for common exceptions
        if (exception is ArgumentException)
        {
            await WriteBadRequestResponse(httpContext, exception.Message, cancellationToken);
            return true;
        }
        // Use structured logging with message templates for better performance
        logger.LogError(exception, "Unhandled exception in {RequestPath}", httpContext.Request.Path);
        // Reuse problem details object to reduce allocations
        var problemDetails = ProblemDetailsPool.Get();
        try
        {
            ConfigureProblemDetails(problemDetails, httpContext, exception);
            await WriteResponse(httpContext, problemDetails, cancellationToken);
        }
        finally
        {
            ProblemDetailsPool.Return(problemDetails);
        }
        return true;
    }
    private static async Task WriteBadRequestResponse(
        HttpContext context, 
        string message, 
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/problem+json";
        
        // Use pre-serialized response for common cases
        var response = $$"""
        {
          "status": 400,
          "title": "Bad Request",
          "detail": "{{message}}",
          "traceId": "{{context.TraceIdentifier}}"
        }
        """;
        
        await context.Response.WriteAsync(response, cancellationToken);
    }
    private static void ConfigureProblemDetails(
        Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails, 
        HttpContext context, 
        Exception exception)
    {
        problemDetails.Status = 500;
        problemDetails.Title = "Internal Server Error";
        problemDetails.Detail = "An unexpected error occurred.";
        problemDetails.Instance = context.Request.Path;
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
    }
    private static async Task WriteResponse(
        HttpContext context, 
        Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails, 
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = problemDetails.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        
        await context.Response.WriteAsJsonAsync(problemDetails, JsonOptions, cancellationToken);
    }
}
// Simple object pool for ProblemDetails to reduce allocations
public static class ProblemDetailsPool
{
    private static readonly ConcurrentQueue<Microsoft.AspNetCore.Mvc.ProblemDetails> Pool = new();
    public static Microsoft.AspNetCore.Mvc.ProblemDetails Get()
    {
        if (Pool.TryDequeue(out var item))
        {
            // Reset the object
            item.Status = null;
            item.Title = null;
            item.Detail = null;
            item.Instance = null;
            item.Extensions?.Clear();
            return item;
        }
        
        return new Microsoft.AspNetCore.Mvc.ProblemDetails { Extensions = new Dictionary<string, object?>() };
    }
    public static void Return(Microsoft.AspNetCore.Mvc.ProblemDetails item)
    {
        if (Pool.Count < 100) // Limit pool size
        {
            Pool.Enqueue(item);
        }
    }
}