using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

// A mentor who has no account yet (an incomplete mentor imported without an email) placed in a team slot.
// It is kept apart from MentorAssignment on purpose: every rule that depends on a real account (access, chat,
// notifications) keeps reading MentorAssignment only. When the mentor's email arrives, this row is replaced by a
// real MentorAssignment on the same team and slot.
public class TemporaryMentorAssignment : AuditableEntity
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid DraftId { get; set; }
    public virtual MentorImportDraft Draft { get; set; } = null!;

    public Guid AssignedById { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public MentorType Slot { get; set; } = MentorType.Enterprise;
    public MentorAssignmentStatus Status { get; set; } = MentorAssignmentStatus.Active;
    public string? Note { get; set; }
}
