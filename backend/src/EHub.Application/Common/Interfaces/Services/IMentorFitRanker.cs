using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

public enum MentorRankingScope
{
    MentorRole,
    AllSpecialties
}

public interface IMentorFitRanker
{
    IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken);

    IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, MentorRankingScope scope, CancellationToken cancellationToken) =>
        Rank(projectText, mentors, cancellationToken);
}

// A relative ranking score, not a calibrated probability of successful mentoring.
public sealed record MentorFitRanking(int Score, string[] Reasons, bool HasEvidence = true, double? RankingValue = null,
    MentorHistoryRankingEvidence? History = null);

public sealed record MentorHistoryRankingEvidence(int BaseFitScore, int CompletedSessionCount,
    int MentoredTeamCount, int AchievementCount, int HistoryBonus, int AchievementBonus);
