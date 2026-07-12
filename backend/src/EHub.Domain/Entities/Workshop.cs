using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Workshop : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkshopType Type { get; set; } = WorkshopType.Other;
    public WorkshopTargetAudience TargetAudience { get; set; } = WorkshopTargetAudience.All;
    public WorkshopFormat Format { get; set; } = WorkshopFormat.Offline;
    public string? BannerUrl { get; set; }
    public string AttachmentsJson { get; set; } = "[]";

    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }

    public string? Location { get; set; }
    public string? MeetingLink { get; set; }

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;

    public WorkshopStatus Status { get; set; } = WorkshopStatus.Draft;

    // Navigation property
    public virtual ICollection<WorkshopAttendance> Attendances { get; set; } = new List<WorkshopAttendance>();
}
