namespace Healthcare.Presentation.API.Responses;

/// <summary>
/// Detailed error response for exception handling.
/// </summary>
/// <remarks>
/// Used by ExceptionHandlingMiddleware to provide
/// detailed error information in development and
/// sanitized errors in production.
/// </remarks>
public sealed class ErrorResponse
{
    /// <summary>
    /// Gets or sets the error type/code.
    /// </summary>
    /// <example>ValidationError</example>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <example>Invalid appointment data provided</example>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of validation errors (if any).
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Gets or sets the stack trace (development only).
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the error.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the request path that caused the error.
    /// </summary>
    public string? Path { get; set; }
}