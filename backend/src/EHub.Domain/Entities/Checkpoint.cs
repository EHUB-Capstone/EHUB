using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Checkpoint : AuditableEntity
{
    public Guid? CourseId { get; set; }
    public virtual Course? Course { get; set; }

    public Guid? ClassId { get; set; }
    public virtual Class? Class { get; set; }

    public string Name { get; set; } = string.Empty;
    public int CheckpointNumber { get; set; }
    public string? Description { get; set; }
    public string RequirementsJson { get; set; } = "[]";

    public DateTime? OpenDate { get; set; }
    public DateTime? DueDate { get; set; }

    public CheckpointStatus Status { get; set; } = CheckpointStatus.Draft;

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    // Navigation properties
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public virtual ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
}
