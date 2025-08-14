using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Diagnostics;

namespace GlobalErrorHandlingAdvantage.Exceptions;

public sealed class ObservableExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ObservableExceptionHandler> _logger;
    private readonly Counter<int> _exceptionCounter;
    private readonly Histogram<double> _exceptionDuration;

    private ObservableExceptionHandler(ILogger<ObservableExceptionHandler> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        var meter = meterFactory.Create("MyApp.Exceptions");
        _exceptionCounter = meter.CreateCounter<int>("exceptions_total", "Total number of exceptions");
        _exceptionDuration = meter.CreateHistogram<double>("exception_handling_duration", "ms", "Exception handling duration");
    }
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Record metrics
            var tags = new TagList
            {
                ["exception_type"] = exception.GetType().Name,
                ["endpoint"] = httpContext.Request.Path,
                ["method"] = httpContext.Request.Method
            };
            
            _exceptionCounter.Add(1, tags);
            // Structured logging with correlation
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = httpContext.TraceIdentifier,
                ["UserId"] = httpContext.User.Identity?.Name,
                ["RequestPath"] = httpContext.Request.Path,
                ["RequestMethod"] = httpContext.Request.Method,
                ["UserAgent"] = httpContext.Request.Headers.UserAgent.FirstOrDefault()
            });
            _logger.LogError(exception, "Unhandled exception occurred");
            var problemDetails = CreateProblemDetails(httpContext, exception);
            httpContext.Response.StatusCode = problemDetails.Status ?? 500;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }
        finally
        {
            stopwatch.Stop();
            _exceptionDuration.Record(stopwatch.ElapsedMilliseconds, 
                new TagList { ["exception_type"] = exception.GetType().Name });
        }
    }
    private static Microsoft.AspNetCore.Mvc.ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = GetStatusCode(exception),
            Title = GetTitle(exception),
            Detail = GetDetail(exception),
            Instance = context.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier,
                ["timestamp"] = DateTime.UtcNow,
                ["correlationId"] = Guid.NewGuid().ToString()
            }
        };
    }
}