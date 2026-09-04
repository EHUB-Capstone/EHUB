using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class SemesterAuditLog : BaseEntity
{
    public Guid SemesterId { get; set; }
    public virtual Semester Semester { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public Guid PerformedByUserId { get; set; }
    public virtual User PerformedByUser { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? DetailsJson { get; set; }
}
