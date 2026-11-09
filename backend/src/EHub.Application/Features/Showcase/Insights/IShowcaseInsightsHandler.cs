using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Insights;

public interface IShowcaseInsightsHandler
{
    Task<Result<ShowcaseInsightsDto>> GetAsync(Guid showcaseId, Guid userId, CancellationToken cancellationToken = default);
}
