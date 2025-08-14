using GlobalErrorHandlingAdvantage.Exceptions;
using GlobalErrorHandlingAdvantage.ProblemDetails;
var builder = WebApplication.CreateBuilder(args);
// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Configure Problem Details
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["requestId"] = context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
        
        // Add user context if available
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            context.ProblemDetails.Extensions["userId"] = 
                context.HttpContext.User.FindFirst("sub")?.Value;
        }
    };
});
// Register exception handlers (processed in order)
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<BusinessRuleExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Register custom problem details writer
builder.Services.AddSingleton<IProblemDetailsWriter, CustomProblemDetailsWriter>();
var app = builder.Build();
// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    
}
else
{
    // Use built-in exception handler middleware
    app.UseExceptionHandler();
    app.UseHsts();
}
// Alternative: Use custom middleware (choose one approach)
// app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();