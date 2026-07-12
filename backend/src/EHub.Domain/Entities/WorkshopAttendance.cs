using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class WorkshopAttendance : AuditableEntity
{
    public Guid WorkshopId { get; set; }
    public virtual Workshop Workshop { get; set; } = null!;

    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }

    public Guid? StudentId { get; set; }
    public virtual Student? Student { get; set; }

    public Guid? ClassId { get; set; }
    public virtual Class? Class { get; set; }

    public WorkshopAttendanceMode Mode { get; set; } = WorkshopAttendanceMode.Offline;
    public WorkshopAttendanceStatus Status { get; set; } = WorkshopAttendanceStatus.Registered;

    public string? EvidenceUrl { get; set; }
    public DateTime? CheckInAt { get; set; }

    public Guid? VerifiedById { get; set; }
    public virtual User? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public string? RejectReason { get; set; }
}
