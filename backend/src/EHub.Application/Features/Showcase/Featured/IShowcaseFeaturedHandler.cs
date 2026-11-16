using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Featured;

public interface IShowcaseFeaturedHandler
{
    Task<Result<ShowcaseFeaturedDto>> SetAsync(Guid showcaseId, bool featured, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Applies the flag to every id the caller may moderate in one transaction; other ids are counted as skipped.</summary>
    Task<Result<BulkFeatureShowcasesResultDto>> SetManyAsync(IReadOnlyCollection<Guid> showcaseIds, bool featured, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Published showcases the caller may feature: all for an admin, only the lecturer's own classes otherwise.</summary>
    Task<Result<IReadOnlyList<FeaturedCandidateDto>>> ListCandidatesAsync(Guid userId, string role, CancellationToken cancellationToken = default);
}
