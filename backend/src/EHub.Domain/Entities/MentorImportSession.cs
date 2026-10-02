using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class MentorImportSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminUserId { get; set; }
    public User AdminUser { get; set; } = null!;
    public Guid SemesterId { get; set; }
    public Semester Semester { get; set; } = null!;
    public string RowsJson { get; set; } = "[]";
    public MentorAdminSessionStatus Status { get; set; } = MentorAdminSessionStatus.Available;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public uint Version { get; set; }
}
