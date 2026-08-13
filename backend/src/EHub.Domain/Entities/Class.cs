using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Class : AuditableEntity
{
    public string ClassCode { get; set; } = string.Empty;
    public int ClassIndex { get; set; }

    public Guid SemesterId { get; set; }
    public virtual Semester Semester { get; set; } = null!;

    public Guid CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    public Guid? PrimaryLecturerId { get; set; }
    public virtual User? PrimaryLecturer { get; set; }

    public string? Room { get; set; }
    public string? ScheduleJson { get; set; }

    public bool IsEnrollmentMajorLocked { get; set; } = false;
    public ClassStatus Status { get; set; } = ClassStatus.Draft;

    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public virtual User? CompletedByUser { get; set; }
    public string? CompletionReason { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }
    public Guid? ArchivedByUserId { get; set; }
    public virtual User? ArchivedByUser { get; set; }
    public ClassStatus? StatusBeforeArchive { get; set; }

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    // PostgreSQL optimistic concurrency token mapped to the system xmin column.
    public uint Version { get; set; }

    // Navigation properties
    public virtual ICollection<ClassLecturer> ClassLecturers { get; set; } = new List<ClassLecturer>();
    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    public virtual ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
    public virtual ICollection<AcademicDataset> AcademicDatasets { get; set; } = new List<AcademicDataset>();
    public virtual ICollection<DataBankImportBatch> DataBankImportBatches { get; set; } = new List<DataBankImportBatch>();
    public virtual ICollection<ChatGroup> ChatGroups { get; set; } = new List<ChatGroup>();
    public virtual ICollection<WorkshopAttendance> WorkshopAttendances { get; set; } = new List<WorkshopAttendance>();
    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public virtual ICollection<WeeklyTask> WeeklyTasks { get; set; } = new List<WeeklyTask>();
    public virtual ICollection<ClassAuditLog> AuditLogs { get; set; } = new List<ClassAuditLog>();
    public virtual ICollection<ClassImportSession> ImportSessions { get; set; } = new List<ClassImportSession>();
}
