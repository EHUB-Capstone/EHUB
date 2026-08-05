using System;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ClassImportSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string ValidRowsJson { get; set; } = "[]";
    public ClassImportSessionStatus Status { get; set; } = ClassImportSessionStatus.Available;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    // PostgreSQL xmin prevents two commit requests from acquiring one session.
    public uint Version { get; set; }
}
