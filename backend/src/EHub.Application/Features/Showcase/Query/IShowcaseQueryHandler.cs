using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Query;

public interface IShowcaseQueryHandler
{
    /// <summary>
    /// Published pages are visible to every signed-in user. Any other status is visible only to the owning team and to
    /// reviewers in scope; everyone else gets "not found" so unpublished pages cannot be discovered.
    /// </summary>
    Task<Result<ShowcaseDetailDto>> GetAsync(
        Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken = default);
}
