using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

public interface IMentorFitRanker
{
    IReadOnlyDictionary<Guid, MentorFitRanking> Rank(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken);
}

// A relative ranking score, not a calibrated probability of successful mentoring.
public sealed record MentorFitRanking(int Score, string[] Reasons, bool HasEvidence = true, double? RankingValue = null);
