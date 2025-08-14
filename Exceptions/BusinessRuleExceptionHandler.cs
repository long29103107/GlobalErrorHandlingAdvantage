using Microsoft.AspNetCore.Diagnostics;

namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class BusinessRuleExceptionHandler : IExceptionHandler
{
    private readonly ILogger<BusinessRuleExceptionHandler> _logger;
    public BusinessRuleExceptionHandler(ILogger<BusinessRuleExceptionHandler> logger)
    {
        _logger = logger;
    }
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BusinessRuleException businessException)
            return false;
        _logger.LogWarning("Business rule violation: {Message}", businessException.Message);
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Business Rule Violation",
            Detail = businessException.Message,
            Type = "https://myapp.com/errors/business-rule-violation",
            Instance = httpContext.Request.Path,
            Extensions = 
            { 
                ["traceId"] = httpContext.TraceIdentifier,
                ["ruleType"] = businessException.GetType().Name
            }
        };
        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}