using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectData;

public interface IGetProjectDataQueryHandler
{
    Task<Result<PagedResponse<ProjectDataItemResponse>>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
