using EHub.Contracts.Dashboard;
using EHub.Shared.Results;

namespace EHub.Application.Features.Dashboard.GetAdminDashboard;

public interface IGetAdminDashboardQueryHandler
{
    Task<Result<AdminDashboardResponse>> HandleAsync(CancellationToken cancellationToken = default);
}
