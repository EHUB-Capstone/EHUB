using System.Text.Json;
using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed class ProjectProposalAnalysisJobProcessor : IProjectProposalAnalysisJobProcessor
{
    private const string SupportedSnapshotSchema = "project-proposal-snapshot-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProposalAnalysisProvider _provider;
    private readonly IProposalSimilarityRetriever _retriever;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProjectProposalAnalysisJobProcessor(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IProposalAnalysisProvider provider,
        IProposalSimilarityRetriever retriever,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _provider = provider;
        _retriever = retriever;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task ProcessAsync(Guid jobId, string leaseOwner, CancellationToken cancellationToken = default)
    {
        var job = await _context.ProjectProposalAnalysisJobs
            .AsNoTracking()
            .Include(candidate => candidate.ProposalVersion)
            .SingleOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        if (job == null
            || job.Status != ProjectProposalAnalysisJobStatus.Processing
            || !string.Equals(job.LeaseOwner, leaseOwner, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(job.ProposalVersion.SnapshotSchemaVersion, SupportedSnapshotSchema, StringComparison.Ordinal))
        {
            throw new ProposalAnalysisProcessingException(
                "PROPOSAL_ANALYSIS_SNAPSHOT_UNSUPPORTED",
                "The proposal snapshot schema is not supported.");
        }

        ProjectProposalSnapshotDto snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ProjectProposalSnapshotDto>(job.ProposalVersion.SnapshotJson, JsonOptions)
                ?? throw new JsonException("The proposal snapshot was empty.");
        }
        catch (JsonException exception)
        {
            throw new ProposalAnalysisProcessingException(
                "PROPOSAL_ANALYSIS_SNAPSHOT_INVALID",
                "The proposal snapshot could not be read.",
                innerException: exception);
        }

        var retrieval = await _retriever.RetrieveAsync(
            job.ProposalVersionId,
            job.IncludeCrossSemester,
            cancellationToken);
        Validate(retrieval);

        var providerResult = await _provider.AnalyzeAsync(new ProposalAnalysisProviderRequest(
            job.Id,
            job.ProposalVersionId,
            snapshot,
            job.CandidateScope,
            job.IncludeCrossSemester,
            job.LanguageMode,
            job.ConfigurationVersion,
            retrieval.Matches.Select(match => new ProposalAnalysisProviderCandidate(
                match.ProposalVersionId,
                match.Proposal,
                match.SemanticSimilarity,
                new ProposalAnalysisProviderFieldScores(
                    match.FieldSimilarities.Problem,
                    match.FieldSimilarities.Solution,
                    match.FieldSimilarities.TargetCustomers,
                    match.FieldSimilarities.ValueAndApproach),
                match.WeightedSemanticSimilarity)).ToArray()), cancellationToken);
        Validate(providerResult);

        await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
        {
            var currentJob = await _context.ProjectProposalAnalysisJobs
                .Include(candidate => candidate.Result)
                .SingleOrDefaultAsync(candidate => candidate.Id == jobId, transactionCancellationToken);

            if (currentJob == null
                || currentJob.Status != ProjectProposalAnalysisJobStatus.Processing
                || !string.Equals(currentJob.LeaseOwner, leaseOwner, StringComparison.Ordinal))
            {
                return false;
            }

            if (currentJob.Result == null)
            {
                var result = new ProjectProposalAnalysisResult
                {
                    AnalysisJobId = currentJob.Id,
                    AnalysisJob = currentJob,
                    Summary = providerResult.Summary.Trim(),
                    OverlapRisk = providerResult.OverlapRisk,
                    PotentialDifferentiatorsJson = JsonSerializer.Serialize(providerResult.PotentialDifferentiators, JsonOptions),
                    LimitationsJson = JsonSerializer.Serialize(providerResult.Limitations, JsonOptions),
                    Provider = providerResult.Provider.Trim(),
                    Model = providerResult.Model.Trim(),
                    PromptVersion = providerResult.PromptVersion.Trim(),
                    OutputSchemaVersion = providerResult.OutputSchemaVersion.Trim(),
                    EmbeddingProvider = retrieval.EmbeddingProvider,
                    EmbeddingModel = retrieval.EmbeddingModel,
                    EmbeddingDimension = retrieval.EmbeddingDimension,
                    TextSchemaVersion = retrieval.TextSchemaVersion,
                    RetrievalVersion = retrieval.RetrievalVersion,
                    FieldTextSchemaVersion = retrieval.FieldTextSchemaVersion,
                    FieldScoringVersion = retrieval.FieldScoringVersion,
                    FieldWeightsJson = JsonSerializer.Serialize(retrieval.FieldWeights, JsonOptions),
                    GeneratedAtUtc = _dateTimeProvider.UtcNow
                };

                foreach (var (match, index) in retrieval.Matches.Select((match, index) => (match, index)))
                {
                    result.Matches.Add(new ProjectProposalAnalysisMatch
                    {
                        AnalysisResult = result,
                        CandidateProposalVersionId = match.ProposalVersionId,
                        Rank = index + 1,
                        SemanticSimilarity = match.SemanticSimilarity,
                        ProblemSimilarity = match.FieldSimilarities.Problem,
                        SolutionSimilarity = match.FieldSimilarities.Solution,
                        TargetCustomerSimilarity = match.FieldSimilarities.TargetCustomers,
                        ValueAndApproachSimilarity = match.FieldSimilarities.ValueAndApproach,
                        WeightedSemanticSimilarity = match.WeightedSemanticSimilarity,
                        CreatedAtUtc = _dateTimeProvider.UtcNow
                    });
                }

                if (retrieval.CurrentTextWasTruncated || retrieval.SkippedCandidateCount > 0)
                {
                    var limitations = providerResult.Limitations.ToList();
                    if (retrieval.CurrentTextWasTruncated)
                        limitations.Add("Nội dung proposal vượt giới hạn embedding và đã được cắt theo quy tắc cố định.");
                    if (retrieval.SkippedCandidateCount > 0)
                        limitations.Add($"Đã bỏ qua {retrieval.SkippedCandidateCount} proposal lịch sử có snapshot không tương thích.");
                    result.LimitationsJson = JsonSerializer.Serialize(limitations.Take(10), JsonOptions);
                }
                currentJob.Result = result;
                _context.ProjectProposalAnalysisResults.Add(result);
            }

            currentJob.Status = ProjectProposalAnalysisJobStatus.Completed;
            currentJob.CompletedAtUtc = _dateTimeProvider.UtcNow;
            currentJob.FailedAtUtc = null;
            currentJob.LeaseOwner = null;
            currentJob.LeaseExpiresAtUtc = null;
            currentJob.LastErrorCode = null;
            await _context.SaveChangesAsync(transactionCancellationToken);
            return true;
        }, cancellationToken);
    }

    private static void Validate(ProposalAnalysisProviderResponse result)
    {
        if (string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Trim().Length > 2_000)
            throw InvalidOutput();
        if (!Enum.IsDefined(result.OverlapRisk))
            throw InvalidOutput();
        if (!ValidItems(result.PotentialDifferentiators) || !ValidItems(result.Limitations))
            throw InvalidOutput();
        if (!ValidMetadata(result.Provider) || !ValidMetadata(result.Model)
            || !ValidMetadata(result.PromptVersion) || !ValidMetadata(result.OutputSchemaVersion))
            throw InvalidOutput();
    }

    private static void Validate(ProposalSimilarityRetrievalResult result)
    {
        if (!ValidMetadata(result.EmbeddingProvider)
            || !ValidMetadata(result.EmbeddingModel)
            || !ValidMetadata(result.TextSchemaVersion)
            || !ValidMetadata(result.RetrievalVersion)
            || !ValidMetadata(result.FieldTextSchemaVersion)
            || !ValidMetadata(result.FieldScoringVersion)
            || result.EmbeddingDimension is <= 0 or > 3072
            || result.Matches.Count > 10
            || result.Matches.Any(match => !ValidSimilarity(match.SemanticSimilarity)
                || !ValidSimilarity(match.FieldSimilarities.Problem)
                || !ValidSimilarity(match.FieldSimilarities.Solution)
                || !ValidSimilarity(match.FieldSimilarities.TargetCustomers)
                || !ValidSimilarity(match.FieldSimilarities.ValueAndApproach)
                || !ValidSimilarity(match.WeightedSemanticSimilarity))
            || !ValidWeight(result.FieldWeights.Problem)
            || !ValidWeight(result.FieldWeights.Solution)
            || !ValidWeight(result.FieldWeights.TargetCustomers)
            || !ValidWeight(result.FieldWeights.ValueAndApproach)
            || Math.Abs(result.FieldWeights.Problem + result.FieldWeights.Solution
                + result.FieldWeights.TargetCustomers + result.FieldWeights.ValueAndApproach - 1d) > 0.000001)
            throw new ProposalAnalysisProcessingException(
                "PROPOSAL_RETRIEVAL_OUTPUT_INVALID",
                "The proposal retrieval result was invalid.");
    }

    private static bool ValidSimilarity(double value) =>
        double.IsFinite(value) && value is >= -1 and <= 1;

    private static bool ValidWeight(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 1;

    private static bool ValidItems(IReadOnlyCollection<string>? items) =>
        items is { Count: <= 10 } && items.All(item => !string.IsNullOrWhiteSpace(item) && item.Trim().Length <= 500);

    private static bool ValidMetadata(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100;

    private static ProposalAnalysisProcessingException InvalidOutput() => new(
        "PROPOSAL_ANALYSIS_OUTPUT_INVALID",
        "The analysis provider returned an invalid result.");
}
