namespace Healthcare.Domain.Common;

/// <summary>
/// Base exception for all domain-specific business rule violations.
/// Used to signal that an invariant or business rule has been broken.
/// </summary>
/// <remarks>
/// This allows the domain layer to express business rule violations
/// without coupling to infrastructure exception handling mechanisms.
/// </remarks>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Gets the error code associated with this domain exception.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="errorCode">A unique code identifying the type of error.</param>
    /// <param name="message">A human-readable error message.</param>
    protected DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class with an inner exception.
    /// </summary>
    /// <param name="errorCode">A unique code identifying the type of error.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    protected DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when an invalid email format is provided.
/// </summary>
public sealed class InvalidEmailException : DomainException
{
    public InvalidEmailException(string email)
        : base("INVALID_EMAIL", $"The email address '{email}' is not valid.")
    {
    }
}

/// <summary>
/// Exception thrown when an invalid phone number format is provided.
/// </summary>
public sealed class InvalidPhoneNumberException : DomainException
{
    public InvalidPhoneNumberException(string phoneNumber)
        : base("INVALID_PHONE", $"The phone number '{phoneNumber}' is not valid.")
    {
    }
}

/// <summary>
/// Exception thrown when an appointment time is invalid (past, outside hours, etc.).
/// </summary>
public sealed class InvalidAppointmentTimeException : DomainException
{
    public InvalidAppointmentTimeException(string reason)
        : base("INVALID_APPOINTMENT_TIME", $"Invalid appointment time: {reason}")
    {
    }
}

/// <summary>
/// Exception thrown when trying to perform an operation on an appointment with invalid status.
/// </summary>
public sealed class InvalidAppointmentStateException : DomainException
{
    public InvalidAppointmentStateException(string operation, string currentStatus)
        : base("INVALID_APPOINTMENT_STATE",
               $"Cannot {operation} an appointment in '{currentStatus}' status.")
    {
    }
}

/// <summary>
/// Exception thrown when a doctor is not available at the requested time.
/// </summary>
public sealed class DoctorNotAvailableException : DomainException
{
    public DoctorNotAvailableException(int doctorId, DateTime time)
        : base("DOCTOR_NOT_AVAILABLE",
               $"Doctor with ID {doctorId} is not available at {time:yyyy-MM-dd HH:mm}.")
    {
    }
}

/// <summary>
/// Exception thrown when an invalid money amount is provided.
/// </summary>
public sealed class InvalidMoneyException : DomainException
{
    public InvalidMoneyException(string reason)
        : base("INVALID_MONEY", $"Invalid money value: {reason}")
    {
    }
}