using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// A reviewer's verdict on one AI file summary ("useful" or "not useful", with a reason for the second). It is the only
/// measure of how good the summaries are on real submissions, so the prompt version and model are copied from the summary:
/// a later change of either can be judged by the ratings that followed it. One row per summary and reviewer.
/// </summary>
public class CheckpointSummaryFeedback : AuditableEntity
{
    public Guid SubmissionFileSummaryId { get; set; }
    public virtual SubmissionFileSummary SubmissionFileSummary { get; set; } = null!;
    public Guid UserId { get; set; }
    public CheckpointSummaryFeedbackRating Rating { get; set; }

    /// <summary>One of a fixed list of reasons, only with <see cref="CheckpointSummaryFeedbackRating.NotHelpful"/>; never free text.</summary>
    public string? Reason { get; set; }

    public string? PromptVersion { get; set; }
    public string? Model { get; set; }
    /// <summary>The exact generation being reviewed; null for historical results created before generation tracking.</summary>
    public Guid? SummaryGenerationId { get; set; }
}
