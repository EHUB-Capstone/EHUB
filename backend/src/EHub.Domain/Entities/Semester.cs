using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Semester : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public SemesterTerm Term { get; set; }
    public int Year { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public SemesterStatus Status { get; set; } = SemesterStatus.Planned;

    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public virtual User? CompletedByUser { get; set; }
    public string? CompletionReason { get; set; }

    // PostgreSQL optimistic concurrency token mapped to xmin.
    public uint Version { get; set; }

    // Navigation properties
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<SemesterAuditLog> AuditLogs { get; set; } = new List<SemesterAuditLog>();
}
