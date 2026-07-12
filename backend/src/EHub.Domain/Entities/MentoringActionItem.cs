using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class MentoringActionItem : AuditableEntity
{
    public Guid MentoringSessionId { get; set; }
    public virtual MentoringSession MentoringSession { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public Guid? AssigneeUserId { get; set; }
    public virtual User? AssigneeUser { get; set; }

    public Guid? AssigneeStudentId { get; set; }
    public virtual Student? AssigneeStudent { get; set; }

    public DateTime? DueDate { get; set; }
    public bool Completed { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
}
