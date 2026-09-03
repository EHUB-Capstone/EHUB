using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class LecturerImportSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminUserId { get; set; }
    public User AdminUser { get; set; } = null!;
    public string RowsJson { get; set; } = "[]";
    public LecturerImportSessionStatus Status { get; set; } = LecturerImportSessionStatus.Available;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    // PostgreSQL xmin prevents two commit requests from consuming one session.
    public uint Version { get; set; }
}
