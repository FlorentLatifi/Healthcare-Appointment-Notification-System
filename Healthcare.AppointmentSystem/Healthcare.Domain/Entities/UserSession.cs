using Healthcare.Domain.Common;

namespace Healthcare.Domain.Entities;

public sealed class UserSession : Entity
{
    public int UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateTime LastUsedAt { get; private set; }
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Domain convenience only. Never use in EF Core LINQ — query <see cref="RevokedAt"/> instead
    /// (<c>RevokedAt == null</c> for active sessions).
    /// </summary>
    public bool IsRevoked => RevokedAt.HasValue;

    public User User { get; private set; } = null!;

    private UserSession() { }

    public UserSession(int userId, Guid familyId, string? userAgent, string? ipAddress)
    {
        UserId = userId;
        FamilyId = familyId;
        UserAgent = userAgent;
        IpAddress = ipAddress;
        LastUsedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
