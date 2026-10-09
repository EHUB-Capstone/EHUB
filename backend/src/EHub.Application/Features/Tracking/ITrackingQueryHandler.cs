using EHub.Contracts.Dashboard;
using EHub.Shared.Results;

namespace EHub.Application.Features.Tracking;

public interface ITrackingQueryHandler
{
    Task<Result<AuthStatsResponse>> GetAuthStatsAsync(int days, CancellationToken cancellationToken = default);
    Task<Result<OnlineUsersResponse>> GetOnlineUsersAsync(CancellationToken cancellationToken = default);
}
