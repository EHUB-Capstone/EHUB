using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class MentorAssignment : AuditableEntity
{
    public Guid MentorProfileId { get; set; }
    public virtual MentorProfile MentorProfile { get; set; } = null!;

    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public Guid AssignedById { get; set; }
    public virtual User AssignedBy { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public MentorAssignmentStatus Status { get; set; } = MentorAssignmentStatus.Active;
    public string? Note { get; set; }

    // Navigation properties
    public virtual ICollection<MentoringSession> Sessions { get; set; } = new List<MentoringSession>();
}
