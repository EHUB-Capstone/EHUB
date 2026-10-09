using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public sealed class PasswordResetToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? RequestedIpAddress { get; set; }
    public string? RequestedUserAgent { get; set; }

    public bool IsUsed => UsedAt.HasValue;

    public bool IsExpired(DateTime utcNow)
    {
        return ExpiresAt <= utcNow;
    }
}
