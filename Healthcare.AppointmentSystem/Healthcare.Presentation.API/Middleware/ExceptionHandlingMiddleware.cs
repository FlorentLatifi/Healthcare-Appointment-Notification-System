using System.Net;
using System.Text.Json;
using Healthcare.Domain.Common;
using Healthcare.Presentation.API.Responses;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Presentation.API.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
/// <remarks>
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
            DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict,
            DbUpdateException => (int)HttpStatusCode.Conflict,
            DomainException => (int)HttpStatusCode.BadRequest,
            ArgumentException => (int)HttpStatusCode.InternalServerError,
            InvalidOperationException => (int)HttpStatusCode.InternalServerError,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        // Populate the error code for domain exceptions
        if (exception is DomainException domainEx)
        {
            errorResponse.Code = domainEx.ErrorCode;
        }

        // Override message for concurrency exceptions so the caller gets a clear,
        // actionable error instead of a raw SQL message.
        if (exception is DbUpdateConcurrencyException)
        {
            errorResponse.Message = "The record was modified by another process. Reload and retry.";
        }
        else if (exception is DbUpdateException)
        {
            errorResponse.Message = "This record cannot be removed because related records exist.";
        }

        // For unexpected internal errors (HTTP 5xx), hide the original message
        // in non-development environments to avoid leaking sensitive internals.
        if (context.Response.StatusCode >= 500 && !_environment.IsDevelopment())
        {
            errorResponse.Message = "An internal server error occurred. Please try again later.";
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await context.Response.WriteAsync(json);
    }
}