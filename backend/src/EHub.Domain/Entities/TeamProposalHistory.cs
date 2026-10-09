using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class TeamProposalHistory : BaseEntity
{
    public Guid ProposalId { get; set; }
    public virtual TeamProposal Proposal { get; set; } = null!;
    public TeamProposalStatus? FromStatus { get; set; }
    public TeamProposalStatus ToStatus { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid PerformedByUserId { get; set; }
    public virtual User PerformedByUser { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string SnapshotJson { get; set; } = "{}";
}
