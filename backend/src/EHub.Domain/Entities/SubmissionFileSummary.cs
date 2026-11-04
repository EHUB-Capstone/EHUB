using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// AI summary of one submitted file version. A stored file never changes (a new upload is a new file and version), so
/// each file is summarised at most once and the result is reused. Only staff can read it; it is never used for grading.
/// </summary>
public class SubmissionFileSummary : AuditableEntity
{
    public Guid SubmissionFileId { get; set; }
    public virtual SubmissionFile SubmissionFile { get; set; } = null!;

    // Copied from the file's submission so queue and screen queries need no joins.
    public Guid TeamId { get; set; }
    public Guid CheckpointId { get; set; }
    public int FileVersionNumber { get; set; }

    public CheckpointSummaryStatus Status { get; set; } = CheckpointSummaryStatus.Pending;

    /// <summary>Queued by an upload rather than by a reviewer; reviewers' requests always run first.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>Quick view built without the model (key sentences and checklist evidence), available in seconds.</summary>
    public string? PreviewJson { get; set; }

    public string? ContentJson { get; set; }
    public string? Model { get; set; }
    public string? PromptVersion { get; set; }

    /// <summary>True when the file was longer than the input limits and only its most informative part was read.</summary>
    public bool InputTruncated { get; set; }

    public Guid RequestedById { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    /// <summary>When a Pending summary may be picked up, or until when a Processing summary is leased.</summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    public string? LastErrorCode { get; set; }

    // PostgreSQL xmin protects concurrent requests for the same file.
    public uint RowVersion { get; set; }
}
