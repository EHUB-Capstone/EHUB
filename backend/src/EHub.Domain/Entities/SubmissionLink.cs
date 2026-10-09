using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class SubmissionLink : AuditableEntity
{
    public Guid SubmissionId { get; set; }
    public virtual Submission Submission { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int VersionNumber { get; set; } = 1;

    public Guid SubmittedById { get; set; }
    public virtual User SubmittedBy { get; set; } = null!;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
