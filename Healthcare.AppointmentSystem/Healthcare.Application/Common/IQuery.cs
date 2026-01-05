namespace Healthcare.Application.Common;

/// <summary>
/// Marker interface for queries (read operations).
/// </summary>
/// <typeparam name="TResponse">The type of data returned by the query.</typeparam>
/// <remarks>
/// Design Pattern: Query Pattern + CQRS
/// 
/// Queries represent requests for data without modifying system state.
/// They are read-only operations.
/// 
/// Examples: GetAppointment, GetAppointmentsByPatient, GetAvailableDoctors
/// 
/// CQRS Principle: Queries don't modify state - they only return data.
/// </remarks>
public interface IQuery<out TResponse>
{
}
