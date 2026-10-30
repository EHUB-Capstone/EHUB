using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

public interface IMentorSemanticReranker
{
    Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RerankAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, IReadOnlyDictionary<Guid, MentorFitRanking> baseline,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, MentorFitRanking>> RerankAsync(string projectText,
        IReadOnlyCollection<MentorProfile> mentors, IReadOnlyDictionary<Guid, MentorFitRanking> baseline,
        MentorRankingScope scope, CancellationToken cancellationToken) =>
        RerankAsync(projectText, mentors, baseline, cancellationToken);
}
