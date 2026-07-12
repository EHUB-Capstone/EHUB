using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Student : AuditableEntity
{
    public string RollNumber { get; set; } = string.Empty;
    public string NormalizedRollNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }

    public string? Major { get; set; }
    public ProgramGroup? ProgramGroup { get; set; }
    public string? AvatarUrl { get; set; }

    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    // Navigation properties
    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
    public virtual ICollection<MentoringActionItem> AssignedMentoringActionItems { get; set; } = new List<MentoringActionItem>();
    public virtual ICollection<MentoringAttendance> MentoringAttendances { get; set; } = new List<MentoringAttendance>();
}
