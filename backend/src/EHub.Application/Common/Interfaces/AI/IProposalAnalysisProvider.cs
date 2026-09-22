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
    double SemanticSimilarity);

public sealed record ProposalAnalysisProviderResponse(
    string Summary,
    ProjectProposalOverlapRisk OverlapRisk,
    IReadOnlyCollection<string> PotentialDifferentiators,
    IReadOnlyCollection<string> Limitations,
    string Provider,
    string Model,
    string PromptVersion,
    string OutputSchemaVersion);

public interface IProposalAnalysisProvider
{
    Task<ProposalAnalysisProviderResponse> AnalyzeAsync(
        ProposalAnalysisProviderRequest request,
        CancellationToken cancellationToken = default);
}
