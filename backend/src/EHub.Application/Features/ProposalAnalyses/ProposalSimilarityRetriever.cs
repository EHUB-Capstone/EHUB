using System.Text.Json;
using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed class ProposalSimilarityRetriever : IProposalSimilarityRetriever
{
    public const string CurrentRetrievalVersion = "proposal-overall-cosine-v1";
    private const string SupportedSnapshotSchema = "project-proposal-snapshot-v1";
    private const int MaximumMatches = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IProposalEmbeddingTextBuilder _textBuilder;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProposalSimilarityRetriever(
        IApplicationDbContext context,
        IProposalEmbeddingTextBuilder textBuilder,
        IEmbeddingProvider embeddingProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _textBuilder = textBuilder;
        _embeddingProvider = embeddingProvider;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ProposalSimilarityRetrievalResult> RetrieveAsync(
        Guid proposalVersionId,
        bool includeCrossSemester,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = await _context.ProjectProposalVersions
            .Include(version => version.Embedding)
            .Include(version => version.ProjectProposal)
                .ThenInclude(proposal => proposal.Class)
            .SingleOrDefaultAsync(version => version.Id == proposalVersionId, cancellationToken)
            ?? throw new ProposalAnalysisProcessingException(
                "PROPOSAL_ANALYSIS_VERSION_NOT_FOUND",
                "The proposal version could not be found.");

        var currentSnapshot = ReadRequiredSnapshot(currentVersion);
        var currentText = _textBuilder.Build(currentSnapshot);

        var candidateVersions = await _context.ProjectProposalVersions
            .Include(version => version.Embedding)
            .Include(version => version.ProjectProposal)
                .ThenInclude(proposal => proposal.Class)
                .ThenInclude(targetClass => targetClass.Semester)
            .Where(version => version.Purpose == ProjectProposalVersionPurpose.Submission
                && version.ProjectProposalId != currentVersion.ProjectProposalId
                && version.ProjectProposal.ProjectId != currentVersion.ProjectProposal.ProjectId
                && version.ProjectProposal.TeamId != currentVersion.ProjectProposal.TeamId)
            .OrderByDescending(version => version.VersionNumber)
            .ToListAsync(cancellationToken);

        candidateVersions = candidateVersions
            .Where(version => includeCrossSemester
                || version.ProjectProposal.Class.SemesterId == currentVersion.ProjectProposal.Class.SemesterId)
            .GroupBy(version => version.ProjectProposalId)
            .Select(group => group.OrderByDescending(version => version.VersionNumber).First())
            .ToList();

        var candidates = new List<CandidateText>(candidateVersions.Count);
        var skippedCandidateCount = 0;
        foreach (var version in candidateVersions)
        {
            if (!TryReadSnapshot(version, out var snapshot))
            {
                skippedCandidateCount++;
                continue;
            }

            candidates.Add(new CandidateText(version, snapshot, _textBuilder.Build(snapshot)));
        }

        var allTexts = new List<VersionText>(candidates.Count + 1)
        {
            new(currentVersion, currentText)
        };
        allTexts.AddRange(candidates.Select(candidate => new VersionText(candidate.Version, candidate.Text)));

        var vectors = await EnsureEmbeddingsAsync(allTexts, cancellationToken);
        var currentVector = vectors[currentVersion.Id];
        var matches = candidates
            .Select(candidate => new ProposalSimilarityCandidate(
                candidate.Version.Id,
                candidate.Version.ProjectProposalId,
                candidate.Version.ProjectProposal.ProjectId,
                candidate.Version.ProjectProposal.TeamId,
                candidate.Version.ProjectProposal.ClassId,
                candidate.Version.ProjectProposal.Class.ClassCode,
                candidate.Version.ProjectProposal.Class.Semester.Code,
                candidate.Version.CreatedAt,
                candidate.Snapshot,
                CosineSimilarity.Calculate(currentVector, vectors[candidate.Version.Id])))
            .OrderByDescending(candidate => candidate.SemanticSimilarity)
            .ThenBy(candidate => candidate.ProposalVersionId)
            .Take(MaximumMatches)
            .ToArray();

        return new ProposalSimilarityRetrievalResult(
            matches,
            _embeddingProvider.ProviderName,
            _embeddingProvider.ModelName,
            _embeddingProvider.Dimension,
            ProposalEmbeddingTextBuilder.CurrentSchemaVersion,
            CurrentRetrievalVersion,
            currentText.WasTruncated,
            skippedCandidateCount);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<float>>> EnsureEmbeddingsAsync(
        IReadOnlyList<VersionText> versions,
        CancellationToken cancellationToken)
    {
        var output = new Dictionary<Guid, IReadOnlyList<float>>(versions.Count);
        var stale = new List<VersionText>();

        foreach (var item in versions)
        {
            if (CanReuse(item.Version.Embedding, item.Text, out var vector))
                output[item.Version.Id] = vector;
            else
                stale.Add(item);
        }

        if (stale.Count == 0) return output;

        IReadOnlyList<EmbeddingVector> generated;
        try
        {
            generated = await _embeddingProvider.GenerateAsync(
                stale.Select(item => item.Text.Text).ToArray(),
                cancellationToken);
        }
        catch (EmbeddingProviderException exception)
        {
            throw new ProposalAnalysisProcessingException(
                exception.ErrorCode,
                "The embedding provider could not process the proposal.",
                exception.IsTransient,
                exception);
        }

        if (generated.Count != stale.Count)
            throw InvalidEmbeddingOutput();

        for (var index = 0; index < stale.Count; index++)
        {
            var values = generated[index].Values.ToArray();
            ValidateVector(values);
            var item = stale[index];
            var embedding = item.Version.Embedding ?? new ProjectProposalEmbedding
            {
                ProposalVersionId = item.Version.Id,
                ProposalVersion = item.Version
            };

            embedding.ContentHash = item.Text.ContentHash;
            embedding.TextSchemaVersion = item.Text.SchemaVersion;
            embedding.Provider = _embeddingProvider.ProviderName;
            embedding.Model = _embeddingProvider.ModelName;
            embedding.Dimension = _embeddingProvider.Dimension;
            embedding.VectorJson = JsonSerializer.Serialize(values, JsonOptions);
            embedding.GeneratedAtUtc = _dateTimeProvider.UtcNow;
            if (item.Version.Embedding == null)
            {
                item.Version.Embedding = embedding;
                _context.ProjectProposalEmbeddings.Add(embedding);
            }

            output[item.Version.Id] = values;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return output;
    }

    private bool CanReuse(
        ProjectProposalEmbedding? embedding,
        CanonicalProposalText text,
        out IReadOnlyList<float> vector)
    {
        vector = Array.Empty<float>();
        if (embedding == null
            || !string.Equals(embedding.ContentHash, text.ContentHash, StringComparison.Ordinal)
            || !string.Equals(embedding.TextSchemaVersion, text.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(embedding.Provider, _embeddingProvider.ProviderName, StringComparison.Ordinal)
            || !string.Equals(embedding.Model, _embeddingProvider.ModelName, StringComparison.Ordinal)
            || embedding.Dimension != _embeddingProvider.Dimension)
            return false;

        try
        {
            var values = JsonSerializer.Deserialize<float[]>(embedding.VectorJson, JsonOptions) ?? [];
            ValidateVector(values);
            vector = values;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ProposalAnalysisProcessingException)
        {
            return false;
        }
    }

    private void ValidateVector(IReadOnlyList<float> values)
    {
        if (values.Count != _embeddingProvider.Dimension
            || values.Any(value => !float.IsFinite(value))
            || !values.Any(value => value != 0))
            throw InvalidEmbeddingOutput();
    }

    private static ProjectProposalSnapshotDto ReadRequiredSnapshot(ProjectProposalVersion version)
    {
        if (!TryReadSnapshot(version, out var snapshot))
            throw new ProposalAnalysisProcessingException(
                "PROPOSAL_ANALYSIS_SNAPSHOT_INVALID",
                "The proposal snapshot could not be read.");
        return snapshot;
    }

    private static bool TryReadSnapshot(ProjectProposalVersion version, out ProjectProposalSnapshotDto snapshot)
    {
        snapshot = new ProjectProposalSnapshotDto();
        if (!string.Equals(version.SnapshotSchemaVersion, SupportedSnapshotSchema, StringComparison.Ordinal))
            return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<ProjectProposalSnapshotDto>(version.SnapshotJson, JsonOptions);
            if (parsed == null) return false;
            snapshot = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProposalAnalysisProcessingException InvalidEmbeddingOutput() => new(
        "PROPOSAL_EMBEDDING_OUTPUT_INVALID",
        "The embedding provider returned an invalid vector.");

    private sealed record VersionText(ProjectProposalVersion Version, CanonicalProposalText Text);
    private sealed record CandidateText(
        ProjectProposalVersion Version,
        ProjectProposalSnapshotDto Snapshot,
        CanonicalProposalText Text);
}
