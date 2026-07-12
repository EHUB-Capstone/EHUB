using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Course : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public CourseStatus Status { get; set; } = CourseStatus.Active;

    // Navigation properties
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
}
