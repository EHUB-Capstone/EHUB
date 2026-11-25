namespace EHub.Contracts.Showcase;

public sealed class CreateShowcaseRuleProposalRequest
{
    public string Phrase { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}

public sealed class ReviewShowcaseRuleProposalRequest
{
    public string Status { get; init; } = string.Empty;
    public uint? RowVersion { get; init; }
}

public sealed class ShowcaseRuleProposalDto
{
    public Guid Id { get; init; }
    public Guid ShowcaseProjectId { get; init; }
    public Guid? SourceReportId { get; init; }
    public Guid? CommentId { get; init; }
    public string? CommentContent { get; set; }
    public string Phrase { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ProposedAtUtc { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public Guid? CreatedRuleId { get; init; }
    public uint RowVersion { get; init; }
}
