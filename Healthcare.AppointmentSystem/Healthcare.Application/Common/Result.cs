namespace Healthcare.Application.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <remarks>
/// Design Pattern: Result Pattern
/// 
/// This pattern is used instead of throwing exceptions for expected failures.
/// It makes the API more explicit about what can go wrong and forces clients
/// to handle both success and failure cases.
/// 
/// Benefits:
/// - No exceptions for business rule violations (better performance)
/// - Explicit error handling (compiler forces you to handle failures)
/// - Can return multiple errors
/// - Better for API responses (convert to HTTP status codes easily)
/// </remarks>
public class Result
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Gets the collection of all errors if multiple failures occurred.
    /// </summary>
    public IReadOnlyCollection<string> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    protected Result(bool isSuccess, string error, IReadOnlyCollection<string>? errors = null)
    {
        if (isSuccess && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException("Success result cannot have an error.");
        }

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException("Failure result must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static Result Success() => new(true, string.Empty);

    /// <summary>
    /// Creates a failure result with a single error.
    /// </summary>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Creates a failure result with multiple errors.
    /// </summary>
    public static Result Failure(IReadOnlyCollection<string> errors)
    {
        var primaryError = errors.FirstOrDefault() ?? "Operation failed.";
        return new Result(false, primaryError, errors);
    }
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// Gets the value returned by the operation (only valid if IsSuccess is true).
    /// </summary>
    public T Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("Cannot access value of a failed result.");
            }

            return _value!;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class.
    /// </summary>
    protected Result(T? value, bool isSuccess, string error, IReadOnlyCollection<string>? errors = null)
        : base(isSuccess, error, errors)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a success result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new(value, true, string.Empty);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public new static Result<T> Failure(string error) => new(default, false, error);

    /// <summary>
    /// Creates a failure result with multiple errors.
    /// </summary>
    public new static Result<T> Failure(IReadOnlyCollection<string> errors)
    {
        var primaryError = errors.FirstOrDefault() ?? "Operation failed.";
        return new Result<T>(default, false, primaryError, errors);
    }

    /// <summary>
    /// Implicitly converts a value to a success result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);
}