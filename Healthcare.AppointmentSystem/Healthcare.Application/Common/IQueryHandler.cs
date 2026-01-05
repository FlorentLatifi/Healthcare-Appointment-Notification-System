namespace Healthcare.Application.Common;

/// <summary>
/// Defines a handler for a query.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle.</typeparam>
/// <typeparam name="TResponse">The type of data returned by the query.</typeparam>
/// <remarks>
/// Design Pattern: Query Pattern + Handler Pattern
/// 
/// Each query has exactly one handler that retrieves the requested data.
/// Query handlers should be optimized for read performance and can bypass
/// domain entities if needed (e.g., read directly from DTOs).
/// </remarks>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles the query asynchronously.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the query execution.</returns>
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}