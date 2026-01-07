namespace Healthcare.Presentation.API.Responses;

/// <summary>
/// Generic API response wrapper for consistent response format.
/// </summary>
/// <typeparam name="T">The type of data returned.</typeparam>
/// <remarks>
/// Design Pattern: Wrapper Pattern
/// 
/// Benefits:
/// - Consistent response structure across all endpoints
/// - Easy error handling for clients
/// - Metadata support (pagination, etc.)
/// 
/// Example Success Response:
/// {
///   "success": true,
///   "data": { ... },
///   "message": "Appointment booked successfully",
///   "errors": null
/// }
/// 
/// Example Error Response:
/// {
///   "success": false,
///   "data": null,
///   "message": "Failed to book appointment",
///   "errors": ["Patient not found", "Doctor unavailable"]
/// }
/// </remarks>
public sealed class ApiResponse<T>
{
    /// <summary>
    /// Gets or sets whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the response data.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets the response message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the list of errors (if any).
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Creates a success response with data.
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates an error response with single error message.
    /// </summary>
    public static ApiResponse<T> ErrorResponse(string error, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message ?? "Operation failed",
            Errors = new List<string> { error }
        };
    }

    /// <summary>
    /// Creates an error response with multiple error messages.
    /// </summary>
    public static ApiResponse<T> ErrorResponse(List<string> errors, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message ?? "Operation failed",
            Errors = errors
        };
    }
}

/// <summary>
/// Non-generic API response for operations that don't return data.
/// </summary>
public sealed class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse SuccessResponse(string? message = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message ?? "Operation completed successfully"
        };
    }

    public static ApiResponse ErrorResponse(string error, string? message = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message ?? "Operation failed",
            Errors = new List<string> { error }
        };
    }

    public static ApiResponse ErrorResponse(List<string> errors, string? message = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message ?? "Operation failed",
            Errors = errors
        };
    }
}