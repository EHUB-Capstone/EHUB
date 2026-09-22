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
    public const string CurrentRetrievalVersion = "proposal-overall-top10-field-rerank-v2";
    private const string SupportedSnapshotSchema = "project-proposal-snapshot-v1";
    private const int MaximumMatches = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IProposalEmbeddingTextBuilder _textBuilder;
    private readonly IProposalFieldEmbeddingTextBuilder _fieldTextBuilder;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProposalSimilarityRetriever(
        IApplicationDbContext context,
        IProposalEmbeddingTextBuilder textBuilder,
        IProposalFieldEmbeddingTextBuilder fieldTextBuilder,
        IEmbeddingProvider embeddingProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _textBuilder = textBuilder;
        _fieldTextBuilder = fieldTextBuilder;
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
            .Include(version => version.FieldEmbeddings)
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
            .Include(version => version.FieldEmbeddings)
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
        var overallCandidates = candidates
            .Select(candidate => new OverallCandidate(
                candidate,
                CosineSimilarity.Calculate(currentVector, vectors[candidate.Version.Id])))
            .OrderByDescending(candidate => candidate.SemanticSimilarity)
            .ThenBy(candidate => candidate.Candidate.Version.Id)
            .Take(MaximumMatches)
            .ToArray();

        var fieldTexts = new List<VersionFieldTexts>(overallCandidates.Length + 1)
        {
            new(currentVersion, _fieldTextBuilder.Build(currentSnapshot))
        };
        fieldTexts.AddRange(overallCandidates.Select(candidate => new VersionFieldTexts(
            candidate.Candidate.Version,
            _fieldTextBuilder.Build(candidate.Candidate.Snapshot))));
        var fieldVectors = await EnsureFieldEmbeddingsAsync(fieldTexts, cancellationToken);

        var matches = overallCandidates
            .Select(candidate => BuildFieldScoredCandidate(candidate, fieldVectors, currentVersion.Id))
            .OrderByDescending(candidate => candidate.WeightedSemanticSimilarity)
            .ThenByDescending(candidate => candidate.SemanticSimilarity)
            .ThenBy(candidate => candidate.ProposalVersionId)
            .ToArray();

        return new ProposalSimilarityRetrievalResult(
            matches,
            _embeddingProvider.ProviderName,
            _embeddingProvider.ModelName,
            _embeddingProvider.Dimension,
            ProposalEmbeddingTextBuilder.CurrentSchemaVersion,
            CurrentRetrievalVersion,
            ProposalFieldEmbeddingTextBuilder.CurrentSchemaVersion,
            WeightedSemanticSimilarity.CurrentVersion,
            WeightedSemanticSimilarity.CurrentWeights,
            currentText.WasTruncated,
            skippedCandidateCount);
    }

    private static ProposalSimilarityCandidate BuildFieldScoredCandidate(
        OverallCandidate overall,
        IReadOnlyDictionary<(Guid VersionId, ProjectProposalSemanticField Field), IReadOnlyList<float>> fieldVectors,
        Guid currentVersionId)
    {
        double Similarity(ProjectProposalSemanticField field) => CosineSimilarity.Calculate(
            fieldVectors[(currentVersionId, field)],
            fieldVectors[(overall.Candidate.Version.Id, field)]);

        var scores = new ProposalFieldSimilarityScores(
            Similarity(ProjectProposalSemanticField.Problem),
            Similarity(ProjectProposalSemanticField.Solution),
            Similarity(ProjectProposalSemanticField.TargetCustomers),
            Similarity(ProjectProposalSemanticField.ValueAndApproach));
        var candidate = overall.Candidate;
        return new ProposalSimilarityCandidate(
            candidate.Version.Id,
            candidate.Version.ProjectProposalId,
            candidate.Version.ProjectProposal.ProjectId,
            candidate.Version.ProjectProposal.TeamId,
            candidate.Version.ProjectProposal.ClassId,
            candidate.Version.ProjectProposal.Class.ClassCode,
            candidate.Version.ProjectProposal.Class.Semester.Code,
            candidate.Version.CreatedAt,
            candidate.Snapshot,
            overall.SemanticSimilarity,
            scores,
            WeightedSemanticSimilarity.Calculate(scores));
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

    private async Task<IReadOnlyDictionary<(Guid VersionId, ProjectProposalSemanticField Field), IReadOnlyList<float>>> EnsureFieldEmbeddingsAsync(
        IReadOnlyList<VersionFieldTexts> versions,
        CancellationToken cancellationToken)
    {
        var output = new Dictionary<(Guid, ProjectProposalSemanticField), IReadOnlyList<float>>();
        var stale = new List<VersionFieldText>();

        foreach (var item in versions)
        {
            var storedByField = item.Version.FieldEmbeddings.ToDictionary(embedding => embedding.Field);
            foreach (var text in item.Texts)
            {
                storedByField.TryGetValue(text.Field, out var embedding);
                if (CanReuse(embedding, text, out var vector))
                    output[(item.Version.Id, text.Field)] = vector;
                else
                    stale.Add(new VersionFieldText(item.Version, text, embedding));
            }
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
                "The embedding provider could not process proposal fields.",
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
            var embedding = item.Embedding ?? new ProjectProposalFieldEmbedding
            {
                ProposalVersionId = item.Version.Id,
                ProposalVersion = item.Version,
                Field = item.Text.Field
            };
            embedding.ContentHash = item.Text.ContentHash;
            embedding.TextSchemaVersion = item.Text.SchemaVersion;
            embedding.Provider = _embeddingProvider.ProviderName;
            embedding.Model = _embeddingProvider.ModelName;
            embedding.Dimension = _embeddingProvider.Dimension;
            embedding.VectorJson = JsonSerializer.Serialize(values, JsonOptions);
            embedding.GeneratedAtUtc = _dateTimeProvider.UtcNow;
            if (item.Embedding == null)
            {
                item.Version.FieldEmbeddings.Add(embedding);
                _context.ProjectProposalFieldEmbeddings.Add(embedding);
            }

            output[(item.Version.Id, item.Text.Field)] = values;
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

    private bool CanReuse(
        ProjectProposalFieldEmbedding? embedding,
        CanonicalProposalFieldText text,
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
    private sealed record OverallCandidate(CandidateText Candidate, double SemanticSimilarity);
    private sealed record VersionFieldTexts(
        ProjectProposalVersion Version,
        IReadOnlyList<CanonicalProposalFieldText> Texts);
    private sealed record VersionFieldText(
        ProjectProposalVersion Version,
        CanonicalProposalFieldText Text,
        ProjectProposalFieldEmbedding? Embedding);
}
