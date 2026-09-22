using EHub.Contracts.ProjectProposals;
using EHub.Domain.Enums;

namespace EHub.Application.Common.Interfaces.AI;

public sealed record ProposalAnalysisProviderRequest(
    Guid AnalysisJobId,
    Guid ProposalVersionId,
    ProjectProposalSnapshotDto Proposal,
    ProjectProposalAnalysisCandidateScope CandidateScope,
    bool IncludeCrossSemester,
    ProjectProposalAnalysisLanguageMode LanguageMode,
    string ConfigurationVersion,
    IReadOnlyList<ProposalAnalysisProviderCandidate> RetrievalCandidates);

public sealed record ProposalAnalysisProviderCandidate(
    Guid ProposalVersionId,
    ProjectProposalSnapshotDto Proposal,
    double SemanticSimilarity,
    ProposalAnalysisProviderFieldScores FieldSimilarities,
    double WeightedSemanticSimilarity,
    double TfIdfSimilarity,
    double JaccardSimilarity,
    double HybridSimilarity);

public sealed record ProposalAnalysisProviderFieldScores(
    double Problem,
    double Solution,
    double TargetCustomers,
    double ValueAndApproach);

public sealed record ProposalAnalysisProviderResponse(
    string Summary,
    ProjectProposalOverlapRisk OverlapRisk,
    IReadOnlyCollection<string> PotentialDifferentiators,
    IReadOnlyCollection<string> Limitations,
    IReadOnlyCollection<ProposalAnalysisProviderMatch> Matches,
    string Provider,
    string Model,
    string PromptVersion,
    string OutputSchemaVersion);

public sealed record ProposalAnalysisProviderMatch(
    Guid ProposalVersionId,
    IReadOnlyCollection<string> Similarities,
    IReadOnlyCollection<string> Differences,
    IReadOnlyCollection<string> NovelElements,
    IReadOnlyCollection<ProposalAnalysisProviderEvidence> Evidence);

public sealed record ProposalAnalysisProviderEvidence(
    ProposalAnalysisEvidenceSource Source,
    string Quote);

public enum ProposalAnalysisEvidenceSource
{
    Current = 1,
    Candidate = 2
}

public interface IProposalAnalysisProvider
{
    Task<ProposalAnalysisProviderResponse> AnalyzeAsync(
        ProposalAnalysisProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProposalAnalysisProviderException : Exception
{
    public ProposalAnalysisProviderException(
        string errorCode,
        string message,
        bool isTransient,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }
    public bool IsTransient { get; }
}
