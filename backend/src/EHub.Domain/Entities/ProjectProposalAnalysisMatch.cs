using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class ProjectProposalAnalysisMatch : BaseEntity
{
    public Guid AnalysisResultId { get; set; }
    public virtual ProjectProposalAnalysisResult AnalysisResult { get; set; } = null!;

    public Guid CandidateProposalVersionId { get; set; }
    public virtual ProjectProposalVersion CandidateProposalVersion { get; set; } = null!;

    public int Rank { get; set; }
    public double SemanticSimilarity { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
