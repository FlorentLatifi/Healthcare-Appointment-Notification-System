using System.Net;
using System.Text.Json;
using FluentValidation;
using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Domain.Common;
using Healthcare.Presentation.API.Responses;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Presentation.API.Middleware;

/// <summary>
/// Global exception handling middleware — consistent API errors, no secret leakage in Production.
/// </summary>
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
            var correlationId = context.Items.TryGetValue(CorrelationContext.HttpContextItemKey, out var cid)
                ? cid?.ToString()
                : CorrelationContext.Current;

            _logger.LogError(
                ex,
                "Unhandled exception Path={Path} Method={Method} CorrelationId={CorrelationId}",
                context.Request.Path,
                context.Request.Method,
                correlationId);

            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string? correlationId)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message, type, code) = MapException(exception);

        var errorResponse = new ErrorResponse
        {
            Type = type,
            Message = message,
            Code = code,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            CorrelationId = correlationId
        };

        if (_environment.IsDevelopment())
        {
            errorResponse.StackTrace = exception.StackTrace;
        }

        // Never leak raw SQL / internals for non-development environments on any status.
        if (!_environment.IsDevelopment() && statusCode >= 500)
        {
            errorResponse.Message = "An internal server error occurred. Please try again later.";
        }

        context.Response.StatusCode = statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await context.Response.WriteAsync(json);
    }

    private static (int StatusCode, string Message, string Type, string? Code) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => (
                (int)HttpStatusCode.BadRequest,
                string.Join(" ", validationEx.Errors.Select(e => e.ErrorMessage).Distinct()),
                nameof(ValidationException),
                "VALIDATION_ERROR"),

            DbUpdateConcurrencyException => (
                (int)HttpStatusCode.Conflict,
                "The record was modified by another process. Reload and retry.",
                nameof(DbUpdateConcurrencyException),
                "CONCURRENCY_CONFLICT"),

            DbUpdateException dbEx when DbConstraintErrors.IsUniqueViolation(dbEx) => (
                (int)HttpStatusCode.Conflict,
                "A conflicting record already exists.",
                nameof(DbUpdateException),
                "UNIQUE_CONSTRAINT"),

            DbUpdateException dbEx when DbConstraintErrors.IsForeignKeyViolation(dbEx) => (
                (int)HttpStatusCode.Conflict,
                "This record cannot be removed because related records exist.",
                nameof(DbUpdateException),
                "FOREIGN_KEY_CONSTRAINT"),

            DbUpdateException => (
                (int)HttpStatusCode.Conflict,
                "The data could not be saved due to a database constraint.",
                nameof(DbUpdateException),
                "DB_CONSTRAINT"),

            DomainException domainEx => (
                (int)HttpStatusCode.BadRequest,
                domainEx.Message,
                domainEx.GetType().Name,
                domainEx.ErrorCode),

            UnauthorizedAccessException uaEx => (
                (int)HttpStatusCode.Unauthorized,
                uaEx.Message,
                nameof(UnauthorizedAccessException),
                null),

            KeyNotFoundException knf => (
                (int)HttpStatusCode.NotFound,
                knf.Message,
                nameof(KeyNotFoundException),
                null),

            ArgumentException argEx => (
                (int)HttpStatusCode.BadRequest,
                argEx.Message,
                nameof(ArgumentException),
                null),

            InvalidOperationException invOp => (
                (int)HttpStatusCode.BadRequest,
                invOp.Message,
                nameof(InvalidOperationException),
                null),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                exception.Message,
                exception.GetType().Name,
                null)
        };
    }
}
