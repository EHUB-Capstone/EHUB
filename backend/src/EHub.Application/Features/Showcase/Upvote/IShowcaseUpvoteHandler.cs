using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Upvote;

public interface IShowcaseUpvoteHandler
{
    Task<Result<ShowcaseUpvoteDto>> SetAsync(Guid showcaseId, Guid userId, string role, bool upvoted, CancellationToken cancellationToken = default);
}
