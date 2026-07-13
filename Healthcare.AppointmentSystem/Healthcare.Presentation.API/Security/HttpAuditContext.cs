using System.Security.Claims;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Audit;
using Healthcare.Presentation.API.Authorization;

namespace Healthcare.Presentation.API.Security;

/// <summary>
/// Resolves actor / client metadata from the current HTTP request for immutable audit rows.
/// </summary>
public sealed class HttpAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Http => _httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => Http?.User;

    public int? ActorUserId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? ActorRole
    {
        get
        {
            try
            {
                return User?.Identity?.IsAuthenticated == true ? User.GetRole() : null;
            }
            catch
            {
                return User?.FindFirst(ClaimTypes.Role)?.Value;
            }
        }
    }

    public string? ClientIp
    {
        get
        {
            var ip = Http?.Connection.RemoteIpAddress?.ToString();
            // Prefer first X-Forwarded-For hop only when proxies are trusted (ForwardedHeaders middleware).
            return string.IsNullOrWhiteSpace(ip) ? null : ip;
        }
    }

    public string? UserAgent
    {
        get
        {
            var ua = Http?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? null : ua;
        }
    }

    public string? CorrelationId =>
        CorrelationContext.Current
        ?? (Http?.Items.TryGetValue(CorrelationContext.HttpContextItemKey, out var cid) == true
            ? cid?.ToString()
            : null);
}
