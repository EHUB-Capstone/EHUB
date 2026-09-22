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
    public DateTime GeneratedAtUtc { get; init; }
}
