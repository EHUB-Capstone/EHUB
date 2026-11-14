using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.History;
using EHub.Application.Features.Mentoring.Scoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.Extensions.Options;

namespace EHub.Application.Features.Mentoring;

/// <summary>Uses the configured model and recorded mentoring evidence for both suggestions and allocation.</summary>
public sealed class MentorRecommendationRanker(IApplicationDbContext context,
    IGenerativeMentorFitRanker generativeRanker, IMentorEvidenceGuard evidenceGuard,
    IDateTimeProvider? dateTimeProvider = null, IOptions<MentorScoringOptions>? scoringOptions = null,
    MentorRankingExecution? execution = null)
    : IMentorRecommendationRanker, IBatchMentorRecommendationRanker
{
    private HashSet<Guid>? historyMentorIds;
    private IReadOnlyDictionary<Guid, MentorHistoryEvidence> history = new Dictionary<Guid, MentorHistoryEvidence>();

    public TimeSpan RequestTimeout => generativeRanker.RequestTimeout;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankForAllocationBatchAsync(
        IReadOnlyCollection<MentorRankingProject> projects, IReadOnlyCollection<MentorProfile> mentors,
        CancellationToken cancellationToken)
    {
        if (projects.Count == 0) return new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>();
        return await RankGenerativeByTypeAsync(projects, mentors, MentorRankingScope.AllSpecialties, cancellationToken);
    }

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
        var result = await RankGenerativeByTypeAsync([new MentorRankingProject(Guid.Empty, projectText)],
            mentors, scope, cancellationToken);
        return result[Guid.Empty];
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankGenerativeByTypeAsync(
        IReadOnlyCollection<MentorRankingProject> projects, IReadOnlyCollection<MentorProfile> mentors,
        MentorRankingScope scope, CancellationToken cancellationToken)
    {
        if (projects.Any(project => project.RequiredMentorTypes?.Any(type => !Enum.IsDefined(type)) == true))
            throw new MentorFitRankerUnavailableException();
        var merged = projects.ToDictionary(project => project.Id, _ => new Dictionary<Guid, MentorFitRanking>());
        if (execution is not null)
        {
            var totalBatches = new[] { MentorType.Enterprise, MentorType.Academic }
                .Where(type => mentors.Any(mentor => mentor.Type == type))
                .Sum(type => (projects.Count(project => project.RequiredMentorTypes is null || project.RequiredMentorTypes.Contains(type))
                    + MentorRankingExecution.ProjectsPerBatch - 1) / MentorRankingExecution.ProjectsPerBatch);
            await execution.BeginAsync(totalBatches, cancellationToken);
        }
        foreach (var type in new[] { MentorType.Enterprise, MentorType.Academic })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pending = projects.Where(project => project.RequiredMentorTypes is null || project.RequiredMentorTypes.Contains(type)).ToArray();
            var candidates = mentors.Where(mentor => mentor.Type == type).ToArray();
            if (pending.Length == 0 || candidates.Length == 0) continue;
            // Separate role requests prevent a strong IT shortlist from silently omitting the business slot.
            // Both requests share the caller's overall deadline; any failure aborts the complete preview.
            var scores = await RankGenerativeAsync(pending, candidates, scope, cancellationToken);
            foreach (var project in pending)
                foreach (var score in scores[project.Id]) merged[project.Id].Add(score.Key, score.Value);
        }
        return merged.ToDictionary(item => item.Key, item => (IReadOnlyDictionary<Guid, MentorFitRanking>)item.Value);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankGenerativeAsync(
        IReadOnlyCollection<MentorRankingProject> projects, IReadOnlyCollection<MentorProfile> mentors,
        MentorRankingScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mentors.Count == 0)
            return projects.ToDictionary(project => project.Id,
                _ => (IReadOnlyDictionary<Guid, MentorFitRanking>)new Dictionary<Guid, MentorFitRanking>());
        var settings = scoringOptions?.Value ?? new MentorScoringOptions();
        if (!settings.IsValid()) throw new MentorFitRankerUnavailableException();
        var ids = mentors.Select(profile => profile.Id).ToHashSet();
        if (historyMentorIds is null || !historyMentorIds.SetEquals(ids))
        {
            history = await MentorHistoryEvidenceLoader.LoadAsync(context, ids,
                dateTimeProvider?.UtcNow ?? DateTime.UtcNow, cancellationToken);
            historyMentorIds = ids;
        }
        // Never send augmented profiles: historical notes/feedback stay inside E-HUB.
        var rankings = await generativeRanker.RankAsync(projects, mentors, scope, cancellationToken);
        if (rankings.Count != projects.Count || projects.Any(project => !rankings.ContainsKey(project.Id)))
            throw new MentorFitRankerUnavailableException();
        var result = new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>();
        foreach (var project in projects)
        {
            ValidateRankings(mentors, rankings[project.Id]);
            var contradictions = evidenceGuard.FindContradictions(project.Text, mentors, cancellationToken);
            result.Add(project.Id, mentors.ToDictionary(profile => profile.Id, profile =>
            {
                var fit = MentorCombinedScoring.Combine(rankings[project.Id][profile.Id], settings, contradictions.Contains(profile.Id));
                if (!history.TryGetValue(profile.Id, out var evidence)) return fit;
                fit = MentorHistoryEvidenceLoader.AddReasons(fit, evidence);
                return MentorHistoryRanking.Apply(fit, evidence);
            }));
        }
        return result;
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
