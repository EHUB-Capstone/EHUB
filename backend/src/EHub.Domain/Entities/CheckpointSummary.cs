using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// AI summary of the files and links a team currently shows on one checkpoint. Each upload creates a new
/// Submission version, so the summary belongs to the team/checkpoint pair rather than to one Submission.
/// Only staff can read it; it is informational and never used for grading.
/// </summary>
public class CheckpointSummary : AuditableEntity
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid CheckpointId { get; set; }
    public virtual Checkpoint Checkpoint { get; set; } = null!;

    public CheckpointSummaryStatus Status { get; set; } = CheckpointSummaryStatus.Pending;

    /// <summary>Hash of the file versions and links that were (or will be) summarised; a different current hash means the summary is stale.</summary>
    public string SourceHash { get; set; } = string.Empty;

    public string? ContentJson { get; set; }
    public string? Model { get; set; }
    public string? PromptVersion { get; set; }

    /// <summary>True when the documents were longer than the configured input limit and only their start was summarised.</summary>
    public bool InputTruncated { get; set; }

    public Guid RequestedById { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    /// <summary>When a Pending summary may be picked up, or until when a Processing summary is leased.</summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    public string? LastErrorCode { get; set; }

    // PostgreSQL xmin protects concurrent requests for the same team/checkpoint.
    public uint RowVersion { get; set; }
}
