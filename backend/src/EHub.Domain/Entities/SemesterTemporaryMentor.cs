using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

// A mentor who has no account yet (an incomplete mentor) taking part in a semester. It plays the role of
// SemesterStaffAssignment for people who have no user record, and is replaced by one when their email arrives.
public class SemesterTemporaryMentor : AuditableEntity
{
    public Guid SemesterId { get; set; }
    public virtual Semester Semester { get; set; } = null!;

    public Guid DraftId { get; set; }
    public virtual MentorImportDraft Draft { get; set; } = null!;

    public SemesterStaffStatus Status { get; set; } = SemesterStaffStatus.Active;

    // PostgreSQL xmin, so two admins editing the same entry are detected.
    public uint Version { get; set; }
}
