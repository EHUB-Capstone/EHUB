using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Common.Interfaces.Services;

public sealed record MentorRankingProject(Guid Id, string Text)
{
    // Null means both slots; allocation explicitly supplies only the slots still empty after edits/retention.
    public IReadOnlyCollection<MentorType>? RequiredMentorTypes { get; init; }
}

/// <summary>External scoring only; eligibility, history bonuses and persistence remain in Application.</summary>
public interface IGenerativeMentorFitRanker
{
    TimeSpan RequestTimeout { get; }

    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankAsync(
        IReadOnlyCollection<MentorRankingProject> projects, IReadOnlyCollection<MentorProfile> mentors,
        MentorRankingScope scope, CancellationToken cancellationToken);
}

/// <summary>Lets semester allocation send its projects together instead of making one remote call per team.</summary>
public interface IBatchMentorRecommendationRanker
{
    TimeSpan RequestTimeout { get; }

    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankForAllocationBatchAsync(
        IReadOnlyCollection<MentorRankingProject> projects, IReadOnlyCollection<MentorProfile> mentors,
        CancellationToken cancellationToken);
}
