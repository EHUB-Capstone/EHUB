namespace EHub.Domain.Entities;

public class TeamProposalMember
{
    public Guid ProposalId { get; set; }
    public virtual TeamProposal Proposal { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public virtual ClassStudent ClassStudent { get; set; } = null!;
    public bool IsLeader { get; set; }
    public bool IsIncluded { get; set; } = true;
    public bool CountsTowardOpenProposal { get; set; } = true;
}
