using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectProposalReview : BaseEntity
{
    public Guid ProjectProposalId { get; set; }
    public virtual ProjectProposal ProjectProposal { get; set; } = null!;

    public Guid ProposalVersionId { get; set; }
    public virtual ProjectProposalVersion ProposalVersion { get; set; } = null!;

    public ProjectProposalStatus FromStatus { get; set; }
    public ProjectProposalStatus ToStatus { get; set; }
    public string Feedback { get; set; } = string.Empty;

    public Guid ReviewedByUserId { get; set; }
    public virtual User ReviewedByUser { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
}
