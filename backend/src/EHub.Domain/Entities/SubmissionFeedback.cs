using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class SubmissionFeedback : AuditableEntity
{
    public Guid SubmissionId { get; set; }
    public virtual Submission Submission { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    public Guid? ParentFeedbackId { get; set; }
    public virtual SubmissionFeedback? ParentFeedback { get; set; }

    public bool Resolved { get; set; } = false;
    public Guid? ResolvedById { get; set; }
    public virtual User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
