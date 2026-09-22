using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectProposalVersion : BaseEntity
{
    public Guid ProjectProposalId { get; set; }
    public virtual ProjectProposal ProjectProposal { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string SnapshotSchemaVersion { get; set; } = "project-proposal-snapshot-v1";
    public ProjectProposalVersionPurpose Purpose { get; set; } = ProjectProposalVersionPurpose.DraftSave;
    public string? ChangeNote { get; set; }

    public Guid ChangedById { get; set; }
    public virtual User ChangedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ProjectProposalReview> Reviews { get; set; } = new List<ProjectProposalReview>();
    public virtual ProjectProposalAnalysisJob? AnalysisJob { get; set; }
    public virtual ProjectProposalEmbedding? Embedding { get; set; }
    public virtual ICollection<ProjectProposalFieldEmbedding> FieldEmbeddings { get; set; } = new List<ProjectProposalFieldEmbedding>();
}
