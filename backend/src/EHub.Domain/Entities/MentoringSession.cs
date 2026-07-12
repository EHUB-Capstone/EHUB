using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class MentoringSession : AuditableEntity
{
    public Guid MentorAssignmentId { get; set; }
    public virtual MentorAssignment MentorAssignment { get; set; } = null!;

    public Guid? LecturerUserId { get; set; }
    public virtual User? Lecturer { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }

    public string? Location { get; set; }
    public string? MeetingUrl { get; set; }

    public MentoringSessionStatus Status { get; set; } = MentoringSessionStatus.Scheduled;
    public string? Notes { get; set; }

    // Navigation properties
    public virtual ICollection<MentoringActionItem> ActionItems { get; set; } = new List<MentoringActionItem>();
    public virtual ICollection<MentoringAttendance> Attendances { get; set; } = new List<MentoringAttendance>();
}
