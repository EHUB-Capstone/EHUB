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

    // Navigation properties
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}
