using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Views;

public interface IShowcaseViewHandler
{
    Task<Result<ShowcaseViewDto>> RecordAsync(Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken = default);
}
