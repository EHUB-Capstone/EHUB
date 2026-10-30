using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

public interface IMentorRecommendationRanker
{
    Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RankAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RankForAllocationAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, CancellationToken cancellationToken) =>
        RankAsync(projectText, mentors, cancellationToken);
}
