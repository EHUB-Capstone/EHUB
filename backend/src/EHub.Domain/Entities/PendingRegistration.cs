using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class PendingRegistration : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? MajorCode { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public DateTime OtpExpiresAtUtc { get; set; }
    public int FailedAttemptCount { get; set; }
    public int ResendCount { get; set; }
    public DateTime? LastSentAtUtc { get; set; }
    public PendingRegistrationStatus Status { get; set; } = PendingRegistrationStatus.Pending;
    public Guid? CompletedUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    // PostgreSQL optimistic concurrency token mapped to xmin.
    public uint Version { get; set; }
}
