using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Behaviors;

/// <summary>
/// Wraps requests implementing <see cref="ITransactionalRequest"/> in an explicit UoW transaction.
/// Queries and non-marker requests pass through unchanged.
/// </summary>
/// <remarks>
/// Handlers may still call <c>SaveChangesAsync</c> inside the open transaction.
/// On commit, any remaining tracked changes are flushed; on Result failure or exception, rolls back.
/// </remarks>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
            return await next();

        var requestName = typeof(TRequest).Name;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("Transaction started for {RequestName}", requestName);

        try
        {
            var response = await next();

            if (IsFailureResult(response))
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogDebug(
                    "Transaction rolled back for {RequestName} due to Result.Failure",
                    requestName);
                return response;
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            _logger.LogDebug("Transaction committed for {RequestName}", requestName);
            return response;
        }
        catch
        {
            try
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogDebug("Transaction rolled back for {RequestName} due to exception", requestName);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback failed for {RequestName}", requestName);
            }

            throw;
        }
    }

    private static bool IsFailureResult(TResponse response) =>
        response is Result result && result.IsFailure;
}
