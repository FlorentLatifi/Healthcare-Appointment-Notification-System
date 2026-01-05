namespace Healthcare.Application.Common;

/// <summary>
/// Defines a handler for a command.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
/// <remarks>
/// Design Pattern: Command Pattern + Handler Pattern
/// 
/// Each command has exactly one handler that contains the business logic
/// for executing that command. This ensures single responsibility and
/// makes the code easy to test and maintain.
/// </remarks>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command asynchronously.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the command execution.</returns>
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}