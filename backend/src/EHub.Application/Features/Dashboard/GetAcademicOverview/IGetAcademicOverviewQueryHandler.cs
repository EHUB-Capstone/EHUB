using EHub.Contracts.Dashboard;
using EHub.Shared.Results;

namespace EHub.Application.Features.Dashboard.GetAcademicOverview;

public interface IGetAcademicOverviewQueryHandler
{
    Task<Result<AcademicOverviewResponse>> HandleAsync(
        Guid lecturerId,
        GetAcademicOverviewRequest request,
        CancellationToken cancellationToken = default);
}
