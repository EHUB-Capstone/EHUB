using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class ShowcaseModerationRuleProposal : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public Guid? SourceReportId { get; set; }
    public Guid? CommentId { get; set; }
    public string Phrase { get; set; } = string.Empty;
    public string PhraseKey { get; set; } = string.Empty;
    public ShowcaseViolationCategory Category { get; set; }
    public ShowcaseRuleProposalStatus Status { get; set; } = ShowcaseRuleProposalStatus.Pending;
    public Guid? ProposedByUserId { get; set; }
    public DateTime ProposedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? CreatedRuleId { get; set; }
    public uint Version { get; set; }
}
