using EHub.Contracts.ProjectProposals;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed record ProposalSimilarityCandidate(
    Guid ProposalVersionId,
    Guid ProjectProposalId,
    Guid ProjectId,
    Guid TeamId,
    Guid ClassId,
    string ClassCode,
    string SemesterCode,
    DateTime SubmittedAtUtc,
    ProjectProposalSnapshotDto Proposal,
    double SemanticSimilarity);

public sealed record ProposalSimilarityRetrievalResult(
    IReadOnlyList<ProposalSimilarityCandidate> Matches,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingDimension,
    string TextSchemaVersion,
    string RetrievalVersion,
    bool CurrentTextWasTruncated,
    int SkippedCandidateCount);

public interface IProposalSimilarityRetriever
{
    Task<ProposalSimilarityRetrievalResult> RetrieveAsync(
        Guid proposalVersionId,
        bool includeCrossSemester,
        CancellationToken cancellationToken = default);
}
