using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectDataDistribution;

public interface IGetProjectDataDistributionQueryHandler
{
    Task<Result<ProjectDataDistributionResponse>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
