using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class ProjectProposalVersion : BaseEntity
{
    public Guid ProjectProposalId { get; set; }
    public virtual ProjectProposal ProjectProposal { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string? ChangeNote { get; set; }

    public Guid ChangedById { get; set; }
    public virtual User ChangedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
