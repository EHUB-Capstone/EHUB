using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class MentoringAttendance : AuditableEntity
{
    public Guid MentoringSessionId { get; set; }
    public virtual MentoringSession MentoringSession { get; set; } = null!;

    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }

    public Guid? StudentId { get; set; }
    public virtual Student? Student { get; set; }

    public string? Name { get; set; }
    public string? Email { get; set; }

    public bool Attended { get; set; } = false;
    public DateTime? CheckInAt { get; set; }
}
