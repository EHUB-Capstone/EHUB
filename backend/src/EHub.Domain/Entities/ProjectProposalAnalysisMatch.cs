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
    public double ProblemSimilarity { get; set; }
    public double SolutionSimilarity { get; set; }
    public double TargetCustomerSimilarity { get; set; }
    public double ValueAndApproachSimilarity { get; set; }
    public double WeightedSemanticSimilarity { get; set; }
    public double TfIdfSimilarity { get; set; }
    public double JaccardSimilarity { get; set; }
    public double HybridSimilarity { get; set; }
    public string SimilaritiesJson { get; set; } = "[]";
    public string DifferencesJson { get; set; } = "[]";
    public string NovelElementsJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
}
