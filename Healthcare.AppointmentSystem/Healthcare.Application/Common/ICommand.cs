namespace Healthcare.Application.Common;

/// <summary>
/// Marker interface for commands (write operations).
/// </summary>
/// <remarks>
/// Design Pattern: Command Pattern + CQRS
/// 
/// Commands represent intentions to change the system state.
/// They are write operations that modify data.
/// 
/// Examples: BookAppointment, ConfirmAppointment, CancelAppointment
/// 
/// CQRS Principle: Commands don't return data (except success/failure).
/// Use queries to retrieve data after a command.
/// </remarks>
public interface ICommand
{
}

/// <summary>
/// Represents a command that returns a result.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface ICommand<out TResponse> : ICommand
{
}