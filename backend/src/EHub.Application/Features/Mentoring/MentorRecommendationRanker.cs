using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.History;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Mentoring;

/// <summary>Uses the configured model and recorded mentoring evidence for both suggestions and allocation.</summary>
public sealed class MentorRecommendationRanker(IApplicationDbContext context, IMentorEmbeddingSearch embeddingSearch,
    IMentorFitRanker? trainedRanker = null, IMentorSemanticReranker? semanticReranker = null,
    IDateTimeProvider? dateTimeProvider = null) : IMentorRecommendationRanker
{
    private HashSet<Guid>? historyMentorIds;
    private IReadOnlyDictionary<Guid, MentorHistoryEvidence> history = new Dictionary<Guid, MentorHistoryEvidence>();

    public Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RankAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken) =>
        RankCoreAsync(projectText, mentors, MentorRankingScope.MentorRole, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RankForAllocationAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken) =>
        RankCoreAsync(projectText, mentors, MentorRankingScope.AllSpecialties, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RankCoreAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, MentorRankingScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mentors.Count == 0) return new Dictionary<Guid, MentorFitRanking>();

        var mentorIds = mentors.Select(profile => profile.Id).ToHashSet();
        if (historyMentorIds is null || !historyMentorIds.SetEquals(mentorIds))
        {
            history = await MentorHistoryEvidenceLoader.LoadAsync(context, mentorIds,
                dateTimeProvider?.UtcNow ?? DateTime.UtcNow, cancellationToken);
            historyMentorIds = mentorIds;
        }
        var evidenceProfiles = MentorHistoryEvidenceLoader.ForRanking(mentors, history);
        IReadOnlyDictionary<Guid, MentorFitRanking> rankings;
        if (trainedRanker is not null)
        {
            rankings = scope == MentorRankingScope.MentorRole
                ? trainedRanker.Rank(projectText, evidenceProfiles, cancellationToken)
                : trainedRanker.Rank(projectText, evidenceProfiles, scope, cancellationToken);
            ValidateRankings(mentors, rankings);
            // The semantic provider can quote its input. Historic session notes stay internal;
            // only the public professional profile may be quoted in the returned reasons.
            if (semanticReranker is not null)
                rankings = scope == MentorRankingScope.MentorRole
                    ? await semanticReranker.RerankAsync(projectText, mentors, rankings, cancellationToken)
                    : await semanticReranker.RerankAsync(projectText, mentors, rankings, scope, cancellationToken);
            ValidateRankings(mentors, rankings);
        }
        else
        {
            var similarities = await embeddingSearch.SimilaritiesAsync(projectText, evidenceProfiles, cancellationToken);
            if (mentors.Any(profile => !similarities.TryGetValue(profile.Id, out var similarity) ||
                !double.IsFinite(similarity) || similarity is < -1 or > 1))
                throw new MentorEmbeddingUnavailableException();
            rankings = evidenceProfiles.ToDictionary(profile => profile.Id, profile =>
            {
                var similarity = similarities[profile.Id];
                var fit = MentorFitScorer.Score(profile, projectText, similarity, 0);
                var reasons = fit.Reasons.Where(reason => !reason.StartsWith("Đang phụ trách ", StringComparison.Ordinal)).ToArray();
                return new MentorFitRanking(fit.Score, reasons, reasons.Length > 0);
            });
        }

        return mentors.ToDictionary(profile => profile.Id, profile =>
        {
            if (!history.TryGetValue(profile.Id, out var evidence)) return rankings[profile.Id];
            var fit = MentorHistoryEvidenceLoader.AddReasons(rankings[profile.Id], evidence);
            return scope == MentorRankingScope.AllSpecialties ? MentorHistoryRanking.Apply(fit, evidence) : fit;
        });
    }

    private static void ValidateRankings(IReadOnlyCollection<MentorProfile> mentors,
        IReadOnlyDictionary<Guid, MentorFitRanking> rankings)
    {
        if (mentors.Any(profile => !rankings.TryGetValue(profile.Id, out var ranking) ||
            ranking.Score is < 0 or > 100 || ranking.Reasons is null ||
            (ranking.RankingValue.HasValue && !double.IsFinite(ranking.RankingValue.Value))))
            throw new MentorFitRankerUnavailableException();
    }
}
