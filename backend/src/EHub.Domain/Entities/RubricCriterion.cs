using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class RubricCriterion : AuditableEntity
{
    public Guid RubricId { get; set; }
    public virtual Rubric Rubric { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal MaxScore { get; set; } = 10;
    public decimal Weight { get; set; } = 0;
    public int DisplayOrder { get; set; }
}
