using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Submission : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid CheckpointId { get; set; }
    public virtual Checkpoint Checkpoint { get; set; } = null!;

    public Guid? SubmittedById { get; set; }
    public virtual User? SubmittedBy { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public int VersionNumber { get; set; } = 1;
    // PostgreSQL xmin protects version assignment when two uploads target the same draft.
    public uint RowVersion { get; set; }

    // Navigation properties
    public virtual ICollection<SubmissionFile> Files { get; set; } = new List<SubmissionFile>();
    public virtual ICollection<SubmissionRequirementContent> RequirementContents { get; set; } = new List<SubmissionRequirementContent>();
    public virtual ICollection<SubmissionFeedback> Feedbacks { get; set; } = new List<SubmissionFeedback>();
    public virtual ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
