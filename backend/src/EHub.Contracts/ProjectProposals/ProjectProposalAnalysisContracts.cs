namespace EHub.Contracts.ProjectProposals;

public sealed class ProjectProposalAnalysisDto
{
    public Guid JobId { get; init; }
    public Guid ProposalVersionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public string CandidateScope { get; init; } = string.Empty;
    public bool IncludeCrossSemester { get; init; }
    public string LanguageMode { get; init; } = string.Empty;
    public DateTime RequestedAtUtc { get; init; }
    public DateTime? ProcessingStartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? FailedAtUtc { get; init; }
    public string? FailureCode { get; init; }
    public bool CanViewDetailedReport { get; init; }
    public ProjectProposalAnalysisReportDto? Report { get; init; }
}

public sealed class ProjectProposalAnalysisReportDto
{
    public string Summary { get; init; } = string.Empty;
    public string OverlapRisk { get; init; } = string.Empty;
    public IReadOnlyCollection<string> PotentialDifferentiators { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Limitations { get; init; } = Array.Empty<string>();
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public string OutputSchemaVersion { get; init; } = string.Empty;
    public string EmbeddingProvider { get; init; } = string.Empty;
    public string EmbeddingModel { get; init; } = string.Empty;
    public int EmbeddingDimension { get; init; }
    public string TextSchemaVersion { get; init; } = string.Empty;
    public string RetrievalVersion { get; init; } = string.Empty;
    public string FieldTextSchemaVersion { get; init; } = string.Empty;
    public string FieldScoringVersion { get; init; } = string.Empty;
    public ProjectProposalFieldWeightsDto FieldWeights { get; init; } = new();
    public string LexicalScoringVersion { get; init; } = string.Empty;
    public string HybridScoringVersion { get; init; } = string.Empty;
    public ProjectProposalHybridWeightsDto HybridWeights { get; init; } = new();
    public int RetrievalCandidateCount { get; init; }
    public ProjectProposalSnapshotDto? CurrentProposal { get; init; }
    public IReadOnlyCollection<ProjectProposalAnalysisMatchDto> Matches { get; init; } = Array.Empty<ProjectProposalAnalysisMatchDto>();
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class ProjectProposalAnalysisMatchDto
{
    public int Rank { get; init; }
    public Guid ProposalVersionId { get; init; }
    public Guid ProjectProposalId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid TeamId { get; init; }
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string SemesterCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StartupName { get; init; } = string.Empty;
    public ProjectProposalSnapshotDto? CandidateProposal { get; init; }
    public double SemanticSimilarity { get; init; }
    public double ProblemSimilarity { get; init; }
    public double SolutionSimilarity { get; init; }
    public double TargetCustomerSimilarity { get; init; }
    public double ValueAndApproachSimilarity { get; init; }
    public double WeightedSemanticSimilarity { get; init; }
    public double TfIdfSimilarity { get; init; }
    public double JaccardSimilarity { get; init; }
    public double HybridSimilarity { get; init; }
    public IReadOnlyCollection<string> Similarities { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Differences { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> NovelElements { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<ProjectProposalAnalysisEvidenceDto> Evidence { get; init; } = Array.Empty<ProjectProposalAnalysisEvidenceDto>();
    public DateTime SubmittedAtUtc { get; init; }
}

public sealed class ProjectProposalAnalysisEvidenceDto
{
    public string Source { get; init; } = string.Empty;
    public string Quote { get; init; } = string.Empty;
}

public sealed class ProjectProposalFieldWeightsDto
{
    public double Problem { get; init; }
    public double Solution { get; init; }
    public double TargetCustomers { get; init; }
    public double ValueAndApproach { get; init; }
}

public sealed class ProjectProposalHybridWeightsDto
{
    public double Semantic { get; init; }
    public double TfIdf { get; init; }
    public double Jaccard { get; init; }
}
