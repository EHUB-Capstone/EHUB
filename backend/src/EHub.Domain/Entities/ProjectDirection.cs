using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectDirection : AuditableEntity
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ProjectDirectionStatus Status { get; set; } = ProjectDirectionStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public virtual User? ReviewedByUser { get; set; }
    public uint Version { get; set; }
    public virtual ICollection<ProjectDirectionReview> Reviews { get; set; } = new List<ProjectDirectionReview>();
}
