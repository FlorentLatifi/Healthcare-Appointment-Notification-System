using System.Net;
using System.Security.Claims;

namespace Healthcare.Presentation.API.Security;

/// <summary>
/// Resolves client identity keys for rate limiting after <see cref="Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware"/>.
/// Prefer authenticated user id (fair multi-user NAT); fall back to remote IP.
/// </summary>
public static class ClientIpResolver
{
    /// <summary>
    /// Partition key for global API rate limiting.
    /// </summary>
    public static string GetRateLimitPartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
            return $"user:{userId}";

        return $"ip:{GetClientIp(httpContext)}";
    }

    /// <summary>
    /// Partition key for unauthenticated auth endpoints (login/register).
    /// Uses IP only — identity is not yet established.
    /// </summary>
    public static string GetAnonymousAuthPartitionKey(HttpContext httpContext)
        => $"auth-ip:{GetClientIp(httpContext)}";

    public static string GetClientIp(HttpContext httpContext)
    {
        // After UseForwardedHeaders(), RemoteIpAddress is the original client when proxy is trusted.
        var ip = httpContext.Connection.RemoteIpAddress;
        if (ip is null)
            return "unknown";

        // Normalize IPv4-mapped IPv6 so the same client does not get two buckets.
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        return ip.ToString();
    }

    public static bool LooksLikePrivateOrLoopback(IPAddress? ip)
    {
        if (ip is null) return true;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
               || (bytes[0] == 192 && bytes[1] == 168);
    }
}
