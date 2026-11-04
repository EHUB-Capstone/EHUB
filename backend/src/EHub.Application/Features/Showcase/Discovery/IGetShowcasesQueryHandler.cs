using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Discovery;

public interface IGetShowcasesQueryHandler
{
    Task<Result<PagedResponse<ShowcaseCardDto>>> HandleAsync(GetShowcasesRequest request, CancellationToken cancellationToken = default);
    Task<Result<ShowcaseFilterOptionsDto>> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
