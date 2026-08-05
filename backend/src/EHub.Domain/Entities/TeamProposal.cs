using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class TeamProposal : AuditableEntity
{
    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;
    public Guid ProposedByStudentId { get; set; }
    public virtual Student ProposedByStudent { get; set; } = null!;
    public string TeamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProjectName { get; set; }
    public TeamProposalStatus Status { get; set; } = TeamProposalStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public virtual User? ReviewedByUser { get; set; }
    public string? LatestReviewComment { get; set; }
    public Guid? ApprovedTeamId { get; set; }
    public virtual Team? ApprovedTeam { get; set; }
    public uint Version { get; set; }
    public virtual ICollection<TeamProposalMember> Members { get; set; } = new List<TeamProposalMember>();
    public virtual ICollection<TeamProposalHistory> History { get; set; } = new List<TeamProposalHistory>();
}
