using EHub.Domain.Entities;

namespace EHub.Application.Features.Showcase.Upvote;

public interface IShowcaseUpvoteStore
{
    // The caller must hold a transaction until the vote and counter changes are committed.
    Task<ShowcaseProject?> LockPublishedAsync(Guid showcaseId, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(Guid showcaseId, Guid userId, DateTime createdAt, CancellationToken cancellationToken);
}
