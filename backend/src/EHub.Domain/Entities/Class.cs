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

    public string? Room { get; set; }
    public string? ScheduleJson { get; set; }

    public bool IsMajorLocked { get; set; } = false;
    public ClassStatus Status { get; set; } = ClassStatus.Active;

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    // Navigation properties
    public virtual ICollection<ClassLecturer> ClassLecturers { get; set; } = new List<ClassLecturer>();
    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    public virtual ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
    public virtual ICollection<AcademicDataset> AcademicDatasets { get; set; } = new List<AcademicDataset>();
    public virtual ICollection<DataBankImportBatch> DataBankImportBatches { get; set; } = new List<DataBankImportBatch>();
}
