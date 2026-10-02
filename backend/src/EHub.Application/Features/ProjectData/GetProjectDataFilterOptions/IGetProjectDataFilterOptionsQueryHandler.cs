using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.GetProjectDataFilterOptions;

public interface IGetProjectDataFilterOptionsQueryHandler
{
    Task<Result<ProjectDataFilterOptionsResponse>> HandleAsync(
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
