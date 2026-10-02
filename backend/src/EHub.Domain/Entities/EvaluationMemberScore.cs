using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

/// <summary>
/// Stores an explicit score override for one team member. When no row exists,
/// the member receives the evaluation's overall score.
/// </summary>
public class EvaluationMemberScore : AuditableEntity
{
    public Guid EvaluationId { get; set; }
    public virtual Evaluation Evaluation { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public decimal Score { get; set; }
}
