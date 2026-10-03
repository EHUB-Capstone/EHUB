using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectDataSummary;

public interface IGetProjectDataSummaryQueryHandler
{
    Task<Result<ProjectDataSummaryResponse>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
