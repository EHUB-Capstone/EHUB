using EHub.Contracts.StartupIndustries;
using EHub.Shared.Results;

namespace EHub.Application.Features.StartupIndustries.ManageStartupIndustries;

public interface IStartupIndustryManagementHandler
{
    Task<Result<StartupIndustryListResponse>> GetAsync(string? search, string? status, string? sort, CancellationToken cancellationToken = default);
    Task<Result<StartupIndustryResponse>> CreateAsync(CreateStartupIndustryRequest request, CancellationToken cancellationToken = default);
    Task<Result<StartupIndustryResponse>> UpdateAsync(Guid id, UpdateStartupIndustryRequest request, CancellationToken cancellationToken = default);
    Task<Result<StartupIndustryResponse>> ChangeStatusAsync(Guid id, ChangeStartupIndustryStatusRequest request, CancellationToken cancellationToken = default);
}
