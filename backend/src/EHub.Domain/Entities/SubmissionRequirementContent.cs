using EHub.Domain.Common;

namespace EHub.Domain.Entities;

/// <summary>
/// A team's answer to one configured requirement in a checkpoint submission.
/// The unique submission/index pair makes saves idempotent for a checkpoint.
/// </summary>
public sealed class SubmissionRequirementContent : AuditableEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public int RequirementIndex { get; set; }
    public string Content { get; set; } = string.Empty;
}
