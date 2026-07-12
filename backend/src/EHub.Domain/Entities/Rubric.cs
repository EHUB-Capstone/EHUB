using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Rubric : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? CourseId { get; set; }
    public virtual Course? Course { get; set; }

    public Guid? ClassId { get; set; }
    public virtual Class? Class { get; set; }

    public Guid? CheckpointId { get; set; }
    public virtual Checkpoint? Checkpoint { get; set; }

    public decimal TotalWeight { get; set; } = 100;

    public RubricStatus Status { get; set; } = RubricStatus.Draft;

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    // Navigation properties
    public virtual ICollection<RubricCriterion> Criteria { get; set; } = new List<RubricCriterion>();
    public virtual ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
