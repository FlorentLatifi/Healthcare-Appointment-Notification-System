using System.Net;
using System.Text.Json;
using Healthcare.Presentation.API.Responses;

namespace Healthcare.Presentation.API.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
/// <remarks>
/// Design Pattern: Middleware Pattern + Chain of Responsibility
/// 
/// This middleware:
/// - Catches ALL unhandled exceptions
/// - Logs errors with details
/// - Returns consistent error responses
/// - Hides sensitive info in production
/// 
/// Error Response Structure:
/// {
///   "type": "ValidationError",
///   "message": "Invalid input data",
///   "errors": ["Email is required"],
///   "timestamp": "2025-01-15T10:30:00Z",
///   "path": "/api/appointments"
/// }
/// </remarks>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception occurred. Path: {Path}, Method: {Method}",
                context.Request.Path,
                context.Request.Method);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Type = exception.GetType().Name,
            Message = exception.Message,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path
        };

        // Include stack trace in development only
        if (_environment.IsDevelopment())
        {
            errorResponse.StackTrace = exception.StackTrace;
        }

        // Set appropriate status code based on exception type
        context.Response.StatusCode = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await context.Response.WriteAsync(json);
    }
}