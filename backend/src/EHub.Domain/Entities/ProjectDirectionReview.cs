using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectDirectionReview : BaseEntity
{
    public Guid ProjectDirectionId { get; set; }
    public virtual ProjectDirection ProjectDirection { get; set; } = null!;
    public ProjectDirectionStatus FromStatus { get; set; }
    public ProjectDirectionStatus ToStatus { get; set; }
    public string Comment { get; set; } = string.Empty;
    public Guid ReviewedByUserId { get; set; }
    public virtual User ReviewedByUser { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
