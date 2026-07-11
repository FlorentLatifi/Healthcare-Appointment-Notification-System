namespace Healthcare.Application.Common;

/// <summary>
/// Marker for requests that must run inside an explicit database transaction
/// (Begin → handler → Commit / Rollback). Prefer commands that mutate state.
/// </summary>
public interface ITransactionalRequest
{
}
