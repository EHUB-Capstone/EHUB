using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class EvaluationDetail : AuditableEntity
{
    public Guid EvaluationId { get; set; }
    public virtual Evaluation Evaluation { get; set; } = null!;

    public Guid RubricCriterionId { get; set; }
    public virtual RubricCriterion RubricCriterion { get; set; } = null!;

    public decimal Score { get; set; }
    public string? Comment { get; set; }
}
